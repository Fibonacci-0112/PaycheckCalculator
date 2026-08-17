# 03 — The Calculation Pipeline

`PaycheckCalculator.Core/Pay/PayCalculator.cs` is the orchestrator for a standard paycheck. It
**composes** the tax steps; it must never absorb them. Gross pay, FICA, federal withholding, and
state withholding are separate collaborators, and state-specific law lives in exactly one place
per state.

---

## Construction and wiring

```csharp
public PayCalculator(
    StateCalculatorRegistry stateRegistry,
    FicaCalculator fica,
    Irs15TPercentageCalculator fed,
    TaxSourceCatalog? sourceCatalog = null)
```

All four are registered as singletons by `AddPaycheckCalculatorCore`
(`Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`). The source catalog is
optional — when null, explanations still render, they just carry no structured citations. That
keeps the calculator constructible in focused unit tests without loading the manifest.

---

## The pipeline, step by step

Entry point: `PaycheckResult Calculate(PaycheckInput input)`.

### Step 0 — Tax-year gate

```csharp
if (!TaxYearSupport.IsSupported(input.TaxYear))
    throw new NotSupportedException(
        $"Tax year {input.TaxYear} is not supported. Only {TaxYearSupport.Default} tax data is currently loaded.");
```

Fails loudly rather than computing 2027 pay against 2026 tables.

### Step 1 — Gross pay

```csharp
var payPeriods = PayPeriods.PerYear(input.Frequency);
var gross = input.PayType == PayType.Salary
    ? (input.SalaryBasis == SalaryBasis.PerYear
        ? input.SalaryAmount / payPeriods
        : input.SalaryAmount)
    : (input.RegularHours * input.HourlyRate)
        + (input.OvertimeHours * input.HourlyRate * input.OvertimeMultiplier);
```

Three cases:

| `PayType` | `SalaryBasis` | Gross for the period |
|---|---|---|
| `Salary` | `PerYear` | `SalaryAmount ÷ payPeriods` |
| `Salary` | `PerPeriod` | `SalaryAmount` |
| `Hourly` | — | `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)` |

Note the overtime formula multiplies the *whole* overtime amount by the multiplier — it is
"overtime hours at 1.5× rate", not "straight time plus a 0.5× premium". Both conventions produce
the same money; this one matches how the UI labels the field.

`gross` stays **unrounded** at this point. That matters for the deduction math that follows.

### Step 2 — Deduction totals and the three tax bases

This is the heart of the design. Four separate sums are taken over the same deduction list:

```csharp
var preTax  = input.Deductions.Where(d => d.Type == DeductionType.PreTax ).Sum(d => d.EffectiveAmount(gross));
var postTax = input.Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.EffectiveAmount(gross));

var preTaxState = input.Deductions
    .Where(d => d.Type == DeductionType.PreTax && d.ReducesStateTaxableWages)
    .Sum(d => d.EffectiveAmount(gross));

var ficaPreTax = input.Deductions
    .Where(d => d.Type == DeductionType.PreTax && d.ReducesFicaWages)
    .Sum(d => d.EffectiveAmount(gross));

var fedPreTax = input.Deductions
    .Where(d => d.Type == DeductionType.PreTax && d.ReducesFederalTaxableWages)
    .Sum(d => d.EffectiveAmount(gross));
```

Yielding three genuinely different wage bases, each floored at zero:

```csharp
var ficaWages  = Math.Max(0m, gross - ficaPreTax);
var fedTaxable = Math.Max(0m, gross - fedPreTax);
// state taxable wages are computed inside the state calculator from
// context.GrossWages - context.PreTaxDeductionsReducingStateWages
```

`preTax` (the full pre-tax total) is used only for display and for the net-pay identity — it is
never a tax base itself.

**Worked example.** Gross $3,000; Section 125 medical $200 (all three flags true); traditional
401(k) $300 (`ReducesFicaWages = false`); Roth 401(k) $150 (all three flags false).

| Base | Deductions applied | Amount |
|---|---|---|
| FICA taxable wages | medical only | $2,800 |
| Federal taxable income | medical + traditional 401(k) | $2,500 |
| State taxable wages | medical + traditional 401(k) | $2,500 |
| `PreTaxDeductions` (display) | all three | $650 |

The Roth contribution reduces net pay by $150 and changes no tax at all — exactly right.

The rationale is spelled out in comments in the source and repeated in the generated
explanation text, so the user sees it too.

### Step 3 — FICA

```csharp
var ficaDetail = _fica.CalculateWithExplanation(
    ficaWages, input.YtdSocialSecurityWages, input.YtdMedicareWages);
var ss       = ficaDetail.SocialSecurity;
var medicare = ficaDetail.Medicare;
var addl     = ficaDetail.AdditionalMedicare;
```

FICA runs **before** federal withholding. It has no dependency on the federal result — the
ordering is simply the order the explanation lines are presented in. Details in
[04 — Federal Withholding & FICA](04-federal-and-fica.md).

### Step 4 — Federal withholding

```csharp
var fedDetail = _fed.CalculateWithExplanation(fedTaxable, input.Frequency, input.FederalW4);
var federal = fedDetail.Withholding;
```

IRS Publication 15-T (2026) Worksheet 1A, percentage method for automated payroll systems:
annualize → apply the built-in standard deduction and W-4 adjustments → graduated brackets →
de-annualize → credits → extra withholding.

### Step 5 — State withholding

Federal must run first, because some states need the federal number:

```csharp
var calc = _stateRegistry.GetCalculator(input.State);
var context = new CommonWithholdingContext(
    input.State,
    gross,
    input.Frequency,
    Year: input.TaxYear,
    PreTaxDeductionsReducingStateWages: preTaxState,
    FederalWithholdingPerPeriod: RoundMoney(federal));

var stateValues = input.StateInputValues ?? new StateInputValues();
var stateResult = calc.Calculate(context, stateValues);
```

Two details:

- **`FederalWithholdingPerPeriod` is passed rounded.** Alabama subtracts annualized federal
  withholding from gross when computing state taxable wages; feeding it the same cent-rounded
  figure the user sees keeps the worksheet reproducible by hand.
- **A null `StateInputValues` becomes an empty bag,** never a null-reference risk for state
  calculators. Every calculator reads through `GetValueOrDefault<T>` with a fallback.

The registry throws `NotSupportedException` for an unregistered state — but
`AddPaycheckCalculatorCore` registers all 51 and `TaxSourceCatalog.ValidateCalculatorRegistrations`
verifies the set at startup, so that path is unreachable in a correctly composed app.

### Step 6 — Net pay and the rounding contract

This is the step most likely to be "simplified" incorrectly. Read it carefully:

```csharp
var net = RoundMoney(gross) - RoundMoney(preTax) - RoundMoney(postTax)
        - RoundMoney(stateResult.Withholding) - RoundMoney(stateResult.DisabilityInsurance)
        - RoundMoney(ss) - RoundMoney(medicare) - RoundMoney(addl) - RoundMoney(federal);
```

where

```csharp
private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
```

**Every component is computed at full precision, then each is rounded to the cent individually,
and net is derived from those rounded components.** The alternative — computing net from
unrounded values and rounding at the end — would produce a displayed paycheck whose lines do not
add up, off by a cent or two. Here the identity

```text
NetPay == GrossPay − PreTaxDeductions − PostTaxDeductions − TotalTaxes
```

holds exactly for the numbers on screen, in the PDF, in the CSV, and in a saved snapshot.

`MidpointRounding.AwayFromZero` (not .NET's default banker's rounding) matches payroll and IRS
convention: $0.005 rounds to $0.01.

### Step 7 — Build the explanation, return the result

Every returned monetary field is passed through `RoundMoney`, and the explanation tree is
attached.

---

## Data flow diagram

```text
PaycheckInput
     │
     ├─▶ gross pay (hourly or salary branch)          [unrounded]
     │
     ├─▶ deduction partitioning ──┬─▶ ficaPreTax  ─▶ ficaWages  = max(0, gross − ficaPreTax)
     │                            ├─▶ fedPreTax   ─▶ fedTaxable = max(0, gross − fedPreTax)
     │                            ├─▶ preTaxState ─▶ (into CommonWithholdingContext)
     │                            ├─▶ preTax      ─▶ (display + net identity)
     │                            └─▶ postTax     ─▶ (net only)
     │
     ├─▶ FicaCalculator(ficaWages, ytdSS, ytdMedicare)
     │        └─▶ ss, medicare, additionalMedicare  + 3 LineExplanations
     │
     ├─▶ Irs15TPercentageCalculator(fedTaxable, frequency, w4)
     │        └─▶ federal                            + 1 LineExplanation
     │
     ├─▶ StateCalculatorRegistry[state].Calculate(context, stateValues)
     │        └─▶ StateWithholdingResult { TaxableWages, Withholding,
     │                                     DisabilityInsurance, Label,
     │                                     optional worksheet steps }
     │
     └─▶ net = Σ rounded components  ─▶ PaycheckResult (+ PaycheckExplanation)
```

---

## Building the explanation tree

`BuildExplanation` assembles an ordered `List<LineExplanation>` — one per visible paycheck row —
and wraps it in a `PaycheckExplanation` along with the source catalog.

Order (the UI renders them in this sequence):

1. **Gross Pay** — hourly (regular + optional overtime) or salary (annual ÷ periods, or
   per-period), built by `BuildGrossExplanation` / `BuildSalaryGrossExplanation`.
2. **Federal Taxable Income** — gross, minus federal-reducing pre-tax, equals base.
3. **FICA Taxable Income** — gross, minus FICA-reducing pre-tax, equals base.
4. **State Taxable Income** — same shape, plus a special case (below).
5. **Federal Withholding** — the Worksheet 1A narrative from the federal calculator.
6. **Social Security**, 7. **Medicare** — from the FICA calculator.
8. **Additional Medicare** — *only when the amount is greater than zero.*
9. **State Income Tax** — the state's own worksheet steps when supplied, else a generic fallback.
10. **State Disability / Paid Leave** — *only when the amount is greater than zero.*
11. **Net Pay** — gross, less pre-tax, less total taxes, less post-tax.

Two conditional lines mean the explanation list length varies by scenario; the UI looks lines up
by `ExplanationLineKey` rather than by index.

### The zero-taxable-wages special case

`BuildStateTaxableIncomeExplanation` handles a situation where naive arithmetic would look
broken:

```csharp
if (taxableWages == 0m && grossPay - preTaxReducing > 0m)
{
    // "No state taxable wages" — a single informational step
}
```

In a no-income-tax state, gross is positive but state taxable wages are zero, so a
"gross − deductions = taxable" narrative would not add up. Instead the user gets one clear step
explaining that the state does not tax these wages.

### Calculator-supplied worksheets

State calculators may opt into telling their own story:

```csharp
if (stateResult.WithholdingSteps is { Count: > 0 })
    return new LineExplanation(
        ExplanationLineKey.StateWithholding,
        $"State Income Tax ({state})",
        stateResult.Withholding,
        stateResult.WithholdingSteps,
        stateResult.WithholdingReference ?? $"{state} state withholding rules (2026).");
```

When a calculator supplies `WithholdingSteps`, `PayCalculator` uses them verbatim — that is how
California's Method B worksheet or Oklahoma's OW-2 table lookup appear step by step. When it does
not, a generic wage-base + final-amount breakdown is synthesized. The same opt-in pattern applies
to `DisabilityInsuranceSteps`.

### Source attribution

Each line is annotated with manifest rule IDs:

```csharp
private static LineExplanation AttachSources(LineExplanation line, IReadOnlyList<string> ruleIds) =>
    ruleIds.Count == 0 ? line : line with { SourceRuleIds = ruleIds };
```

Scopes are resolved per line and always in `TaxCalculationMode.Standard`:

| Line | Jurisdiction | `TaxRuleScope` |
|---|---|---|
| Federal Withholding | `"US"` | `FederalWithholding` |
| Social Security, Medicare | `"US"` | `SocialSecurityMedicare` |
| Additional Medicare | `"US"` | `AdditionalMedicare` |
| State Taxable Income, State Income Tax | the `UsState` | `RegularWithholding` |
| State Disability / Paid Leave | the `UsState` | `PayrollAssessment` |

Gross Pay, the FICA/federal taxable-income lines, and Net Pay carry no rule IDs — they are
arithmetic, not cited law. See
[07 — Explanations & Source Governance](07-explanations-and-source-governance.md).

---

## Composition root

`AddPaycheckCalculatorCore(IServiceCollection, ITaxDataReader)` is the single place the engine is
assembled. In order, it:

1. **Reads eight tax JSON files** through the caller-supplied `ITaxDataReader` —
   `us_irs_15t_2026_percentage_automated.json`, `ar_withholding_2026.json`,
   `ok_ow2_2026_percentage.json`, `ca_method_b_2026.json`, `co_dr0004_2026.json`,
   `connecticut_withholding_2026.json`, `state_supplemental_2026.json`, and
   `tax_source_manifest_2026.json`.

2. **Loads and validates the source catalog** via `TaxSourceCatalog.Load` — a strict gauntlet
   that throws on any malformed or incomplete rule.

3. **Loads every available state schema**, tolerating absences:

   ```csharp
   foreach (var state in Enum.GetValues<UsState>())
   {
       var name = state.ToString().ToLowerInvariant();
       try { schemaJsonMap[state] = dataReader.ReadAllText($"schemas/{name}.json"); }
       catch (FileNotFoundException) { /* provider returns an empty schema */ }
   }
   ```

   This is why `ITaxDataReader` implementations are contractually required to throw
   `FileNotFoundException` for a missing logical name.

4. **Registers shared singletons** — `IStateSchemaProvider`, the reader, the catalog,
   `FicaCalculator`, `Irs15TPercentageCalculator`, and the five data-driven state helpers
   (`ArkansasFormulaCalculator`, `CaliforniaPercentageCalculator`,
   `ColoradoWithholdingCalculator`, `ConnecticutWithholdingCalculator`,
   `OklahomaOw2PercentageCalculator`).

5. **Builds the `StateCalculatorRegistry`** — 44 dedicated calculators, then the seven
   no-income-tax states via `NoIncomeTaxWithholdingAdapter`, then any entries in
   `StateTaxConfigs2026.Configs` (currently empty).

6. **Validates registrations against the manifest:**

   ```csharp
   sourceCatalog.ValidateCalculatorRegistrations(stateRegistry);
   ```

   For every one of the 51 jurisdictions this asserts that exactly one `RegularWithholding` rule
   exists and that its `calculatorClass` string matches the runtime type name of the registered
   calculator. Rename a calculator without updating the manifest and the app fails at startup,
   not in production.

7. **Registers the calculators** — `PayCalculator`, `AnnualProjectionCalculator`,
   `GrossUpCalculator`, `SelfEmploymentCalculator`, `HourlySalaryCalculator`,
   `FederalSupplementalCalculator`, `StateSupplementalCalculator`, `BonusCalculator`,
   `BudgetCalculator`, `BudgetReportCalculator`.

Every registration is a **singleton** built from immutable data. The calculators are stateless
and safe to share across MAUI pages or concurrent Blazor circuits.

### `ITaxDataReader`

```csharp
public interface ITaxDataReader
{
    // Returns the raw JSON for a logical name like "ar_withholding_2026.json"
    // or "schemas/ca.json". Should throw FileNotFoundException when absent so
    // the per-state schema loop can swallow missing files.
    string ReadAllText(string logicalName);
}
```

Two production implementations:

| Host | Implementation | Mechanism |
|---|---|---|
| MAUI | `MauiAppPackageTaxDataReader` | `FileSystem.OpenAppPackageFileAsync(logicalName)` over `MauiAsset` items |
| Blazor | `FileSystemTaxDataReader` | Reads `TaxData/<logicalName>` from `AppContext.BaseDirectory`, translating `/` to the platform separator |

Tests construct calculators directly from files copied next to the test binary. See
[09 — Tax Data Files](09-tax-data-files.md).

---

## Invariants to preserve

If you change `PayCalculator`, these must still hold — several are asserted directly by the test
suite:

1. `NetPay == GrossPay − PreTaxDeductions − PostTaxDeductions − TotalTaxes`, exactly, to the cent.
2. All money is `decimal`, rounded only via `Math.Round(v, 2, MidpointRounding.AwayFromZero)`.
3. Every tax base is floored at zero — negative wages never reach a tax calculator.
4. Federal withholding is computed before state withholding and passed into the state context.
5. No state-specific branching in `PayCalculator`. If a state needs something new, it goes on
   `CommonWithholdingContext` for everyone, or into that state's calculator.
6. Explanation lines are looked up by `ExplanationLineKey`, never by list position.

---

**Next:** [04 — Federal Withholding & FICA](04-federal-and-fica.md)
