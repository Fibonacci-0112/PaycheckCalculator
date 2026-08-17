# 04 — Federal Withholding & FICA

The federal side of the engine: three calculators under `Core/Tax/Federal/` and `Core/Tax/Fica/`.

| Class | File | Authority |
|---|---|---|
| `Irs15TPercentageCalculator` | `Tax/Federal/Irs15TPercentageCalculator.cs` | IRS Pub 15-T (2026), §1 Worksheet 1A + annual percentage-method tables |
| `FicaCalculator` | `Tax/Fica/FicaCalculator.cs` | IRC §3101(a)/(b), §3101(b)(2) |
| `FederalSupplementalCalculator` | `Tax/Federal/FederalSupplementalCalculator.cs` | IRS Pub 15 (Circular E), §7 flat-rate method |

---

## `FederalW4Input`

`Tax/Federal/FederalModels.cs` — the post-2020 Form W-4, modeled field for field.

```csharp
public enum FederalFilingStatus
{
    SingleOrMarriedSeparately,
    MarriedFilingJointly,
    HeadOfHousehold
}

public sealed class FederalW4Input
{
    public FederalFilingStatus FilingStatus { get; init; } = FederalFilingStatus.SingleOrMarriedSeparately;
    public bool    Step2Checked          { get; init; } = false;  // multiple jobs / spouse works
    public decimal Step3TaxCredits       { get; init; } = 0m;     // annual
    public decimal Step4aOtherIncome     { get; init; } = 0m;     // annual
    public decimal Step4bDeductions      { get; init; } = 0m;     // annual
    public decimal Step4cExtraWithholding{ get; init; } = 0m;     // PER PAY PERIOD
}
```

**Step 4(c) is per pay period; Steps 3, 4(a), and 4(b) are annual.** That asymmetry is on the
real form and is reproduced faithfully — Step 3 credits get divided by the period count before
being applied, while Step 4(c) is added to the already-de-annualized amount.

Note there is no allowances field. Allowances were removed from the federal W-4 in 2020. Many
*state* forms still use them, which is why `StateInputValues` carries them per-state.

---

## `Irs15TPercentageCalculator`

### Loading

```csharp
public Irs15TPercentageCalculator(string json)
{
    json = json.Replace("None", "null");
    _data = JsonSerializer.Deserialize<Irs15TRoot>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Failed to load IRS 15-T JSON data.");
}
```

The `"None"` → `"null"` substitution accommodates the source data's Python-flavored sentinel for
an open-ended top bracket. It is a blunt string replacement over the whole document, so avoid
introducing a legitimate `None` string value into that file.

`SupportedTaxYear` is read from the JSON's own `year` field, so the loaded data can be checked
against `TaxYearSupport`.

### The algorithm — Worksheet 1A, 2020+ W-4 branch

`CalculateWithExplanation(decimal taxableWagesThisPeriod, PayFrequency frequency, FederalW4Input w4)`
returns `(decimal Withholding, LineExplanation Explanation)`.

**Short-circuit.** If `taxableWagesThisPeriod <= 0`, return `0` with a "No withholding" step.

**Step 1a — annualize.**
```csharp
var annualWage = taxableWagesThisPeriod * payPeriods;
```

**Step 1e — add other income.**
```csharp
var line1e = annualWage + w4.Step4aOtherIncome;
```

**Step 1g — the built-in standard deduction.**
```csharp
var line1g = w4.Step2Checked
    ? 0m
    : (w4.FilingStatus == FederalFilingStatus.MarriedFilingJointly
        ? _data.WorksheetConstants.Line1G.Mfj
        : _data.WorksheetConstants.Line1G.Other);
```

Checking Step 2 zeroes the built-in deduction — the worksheet's mechanism for keeping two jobs
from each claiming the full deduction. Head of Household shares the "Other" amount with
Single/MFS at this line; the HoH difference shows up in the bracket table instead.

**Step 1h — add W-4 deductions.**
```csharp
var line1h = w4.Step4bDeductions + line1g;
```

**Adjusted annual wage amount** — floored at zero, with a second short-circuit:
```csharp
var adjustedAnnualWage = Math.Max(0m, line1e - line1h);
if (adjustedAnnualWage <= 0m) return (0m, ...);
```

**Step 2 — bracket lookup and tentative annual tax.** Two independent schedules are loaded, and
the Step 2 checkbox selects between them:

```csharp
var schedule = w4.Step2Checked ? _data.AnnualTables.Step2Checked : _data.AnnualTables.Standard;
var brackets = GetBrackets(schedule, w4.FilingStatus);   // SingleOrMfs | MarriedFilingJointly | HeadOfHousehold
var b = FindBracket(brackets, adjustedAnnualWage);

var tentativeAnnual = b.Base + (adjustedAnnualWage - b.ExcessOver) * b.Rate;
```

`FindBracket` walks the ordered list matching `wages >= b.Over && (b.Under is null || wages < b.Under)`,
falling back to the last bracket. `Under == null` marks the open-ended top bracket.

Each bracket carries `Base`, `Rate`, `Over`, `Under`, and `ExcessOver` — mirroring the published
table's "of the amount over" column rather than recomputing cumulative tax.

**De-annualize.**
```csharp
var tentativePerPeriod = tentativeAnnual / payPeriods;
```

**Step 3 — credits**, spread evenly and floored at zero:
```csharp
var creditsPerPeriod = w4.Step3TaxCredits / payPeriods;
var afterCredits = Math.Max(0m, tentativePerPeriod - creditsPerPeriod);
```

**Step 4(c) — extra withholding**, added after the floor so a requested extra amount is never
swallowed:
```csharp
var final = afterCredits + w4.Step4cExtraWithholding;
```

**Round** to the cent, away from zero.

### Worked golden vector

From `VerifiedCalculationCorpusTest`, Single filer, $3,000 biweekly:

```text
$3,000 × 26                        = $78,000    annual wage
− $8,600 (line 1g, Single)         = $69,400    adjusted annual wage
bracket floor $57,900, base $5,800, rate 22%
$5,800 + ($69,400 − $57,900) × .22 = $8,330     tentative annual
$8,330 ÷ 26                        = $320.3846… → $320.38
```

With a $4,400 Step 3 credit and $25 Step 4(c):

```text
($8,330 − $4,400) ÷ 26 + $25 = $176.1538… → $176.15
```

Both are asserted as explicit literals in the test suite.

### A naming caveat

```csharp
// Pub 15-T rounding convention (nearest whole dollar; <50c down, >=50c up)
private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
```

The comment describes Pub 15-T's optional whole-dollar convention, but the code rounds to
**two decimals**. The code is correct for this application — the engine reports cents throughout
so paycheck lines reconcile exactly — and the tests pin cent-level values. Read the code, not the
comment.

---

## `FicaCalculator`

```csharp
public const decimal SocialSecurityRate     = 0.062m;
public const decimal MedicareRate           = 0.0145m;
public const decimal AdditionalMedicareRate = 0.009m;

public decimal SocialSecurityWageBase              { get; init; } = 184_500m;
public decimal AdditionalMedicareEmployerThreshold { get; init; } = 200_000m;
```

Rates are `const` (statutory and stable); the two dollar thresholds are `init` properties so a
test — or a future tax year — can override them without subclassing.

The single input `medicareWagesThisPeriod` is the FICA taxable base computed by `PayCalculator`
(gross minus Section 125-style deductions only). Two YTD figures place this period correctly
against the annual limits.

### Social Security — capped

```csharp
var remainingSsBase = Math.Max(0m, SocialSecurityWageBase - ytdSsWages);
var ssTaxable = Math.Min(medicareWagesThisPeriod, remainingSsBase);
var ss = ssTaxable * SocialSecurityRate;
```

Once YTD SS wages reach $184,500, `remainingSsBase` is zero and Social Security withholding
stops. A period that straddles the cap is split correctly by the `Min`.

### Medicare — uncapped

```csharp
var medicare = medicareWagesThisPeriod * MedicareRate;
```

Flat 1.45% on everything, no wage base.

### Additional Medicare — threshold crossing done right

```csharp
var prior   = ytdMedicareWages;
var current = ytdMedicareWages + medicareWagesThisPeriod;
var over = Math.Max(0m, current - AdditionalMedicareEmployerThreshold)
         - Math.Max(0m, prior   - AdditionalMedicareEmployerThreshold);
var addl = over * AdditionalMedicareRate;
```

This "excess after minus excess before" form is the important part. It taxes only the portion of
*this* period's wages that sits above $200,000, and it behaves correctly in all three cases:

| Situation | `prior` | `current` | `over` |
|---|---|---|---|
| Entirely below the threshold | 150,000 | 155,000 | 0 |
| Straddling the threshold | 198,000 | 205,000 | 5,000 |
| Entirely above | 210,000 | 215,000 | 5,000 |

A naive `Math.Max(0, current − threshold)` would over-tax every period after the crossing.

The threshold is the **employer-side** $200,000 single-employer rule, which is filing-status
independent by design. An employee's actual liability depends on filing status and combined
income and is reconciled on Form 8959 — the generated explanation says exactly that.

### Return shape

```csharp
public sealed record FicaCalculationResult(
    decimal SocialSecurity,
    decimal Medicare,
    decimal AdditionalMedicare,
    LineExplanation SocialSecurityExplanation,
    LineExplanation MedicareExplanation,
    LineExplanation AdditionalMedicareExplanation);
```

A simpler `Calculate(...)` overload returning a `(ss, medicare, addlMedicare)` tuple delegates to
the explanatory version, so there is only one implementation of the math.

`FicaCalculator` is also the **single source of truth for the wage base and threshold used by
self-employment tax** — `SelfEmploymentCalculator` takes a `FicaCalculator` in its constructor
purely to read those two values, so SE tax cannot drift out of sync with FICA.

---

## `FederalSupplementalCalculator`

Used only by the bonus pipeline. Implements the **optional flat-rate method** from Pub 15 §7.

```csharp
public const decimal FlatRate         = 0.22m;   // up to the cumulative $1M
public const decimal MandatoryHighRate = 0.37m;  // above it
public decimal MillionDollarThreshold { get; init; } = 1_000_000m;
```

The $1,000,000 threshold is **cumulative across all supplemental wages one employer pays an
employee during the calendar year**, which is why the calculator needs YTD supplemental wages to
split a payment correctly:

```csharp
var remainingUnderThreshold = Math.Max(0m, MillionDollarThreshold - ytd);
var lowPortion  = Math.Min(wages, remainingUnderThreshold);
var highPortion = wages - lowPortion;

var total = RoundMoney(lowPortion * FlatRate + highPortion * MandatoryHighRate);
```

A $200,000 bonus paid to someone with $900,000 of prior supplemental wages splits as
$100,000 × 22% + $100,000 × 37% = $59,000 — not a flat 22% or a flat 37%.

Both inputs are floored at zero before use.

Note the asymmetry the constant names capture: 22% is *optional* (an employer may instead use the
aggregate method), while 37% above $1M is *mandatory*. This calculator implements the flat-rate
election; states that publish no flat supplemental rate are flagged rather than guessed at — see
[06 — Alternate Calculators](06-alternate-calculators.md#bonuscalculator).

---

## Explanation output

All three calculators emit `LineExplanation` objects with worksheet-shaped
`ExplanationStep`s carrying a label, a plain-language detail, the computed value, and a formula
string with real numbers substituted — e.g.:

```text
Remaining Social Security wage base
  Each year only the first $184,500.00 of wages is subject to SS tax.
  max(0, $184,500.00 − $120,000.00) = $64,500.00
```

Money is formatted with `CultureInfo.GetCultureInfo("en-US")` explicitly, so the strings are
stable regardless of the host's locale — which matters for the PDF/CSV exporters and for tests.

Legacy free-form `Reference` strings are attached at this layer (`"FICA — IRC §3101(a). 2026 wage
base $184,500."`); `PayCalculator` then overlays structured `SourceRuleIds` from the manifest.
See [07 — Explanations & Source Governance](07-explanations-and-source-governance.md).

---

## Tests

| Test file | Covers |
|---|---|
| `VerifiedCalculationCorpusTest` | Pub 15-T golden vectors (annual and biweekly), W-4 adjustment vectors, cross-cutting pipeline invariants |
| `Irs15TExplanationTest` | Worksheet 1A step sequence, labels, and values |
| `FicaExplanationTest` | SS cap, Medicare, Additional Medicare threshold-crossing steps |
| `FederalSupplementalCalculatorTest` | 22%/37% split at the $1M cumulative threshold |
| `ReducesFederalTaxableIncomeTest` | Which deductions move the federal base |
| `TaxYearVersioningTest` | Unsupported-year rejection |

Expected values are written as **explicit numeric literals taken from the published tables** —
never recomputed with production helpers. That is the rule that lets the tests actually catch a
regression rather than agreeing with a broken implementation.

---

**Next:** [05 — The State Withholding Engine](05-state-withholding-engine.md)
