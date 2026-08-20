# 06 — Alternate Calculators

Beyond the standard paycheck, `Core/Pay/` holds five more pipelines. Four are surfaced in both
front-ends through a single **calculation-mode** toggle; the fifth extends every standard result.

| Class | Mode | Input → Output |
|---|---|---|
| `GrossUpCalculator` | Gross-up | `PaycheckInput` + target net → `GrossUpResult` |
| `BonusCalculator` | Bonus / supplemental | `BonusInput` → `BonusResult` |
| `SelfEmploymentCalculator` | Self-employment / 1099 | `SelfEmploymentInput` → `SelfEmploymentResult` |
| `AnnualProjectionCalculator` | (always, on the Annual tab) | `PaycheckInput` + `PaycheckResult` → `AnnualProjection` |
| `HourlySalaryCalculator` | Rate conversion | `HourlySalaryInput` → `HourlySalaryResult` |

All five are registered as singletons by `AddPaycheckCalculatorCore`.

---

## `GrossUpCalculator`

Solves the inverse problem: *given a promised take-home amount, what gross delivers it?* This is
what employers do for net bonuses, relocation payments, and any deal where the employer covers
the tax.

### Why bisection rather than algebra

You cannot invert the forward calculation in closed form. Graduated federal brackets, the Social
Security wage-base cap, the Additional Medicare threshold, percentage-based deductions, and 51
different state formulas all make net-as-a-function-of-gross piecewise and state-dependent.

So the solver treats `PayCalculator.Calculate` as a **monotonic black box** and bisects. Net pay
is non-decreasing in gross — every marginal rate is well under 100% — which is the only property
the search needs.

```csharp
decimal NetAt(decimal gross) => CalculateAt(input, gross).NetPay;
```

Because every probe re-runs the *entire* pipeline, brackets, caps, percentage deductions, and
state rules stay correct automatically. There is no duplicated tax logic to drift.

### The search

**Bracket.** Net is always strictly less than gross for any positive gross (Medicare alone
guarantees a tax), so the target itself is a valid lower bound. The upper bound doubles until its
net clears the target:

```csharp
decimal low = target;
decimal high = target * 2m;
var guard = 0;
while (NetAt(high) < target && guard++ < 200)
{
    low = high;
    high *= 2m;
}
```

**Bisect** to sub-cent precision, maintaining the invariant `net(low) < target ≤ net(high)`:

```csharp
for (var i = 0; i < 100 && high - low > 0.005m; i++)
{
    var mid = (low + high) / 2m;
    if (NetAt(mid) < target) low = mid; else high = mid;
}
```

**Settle on a whole cent.** This last phase is what makes the result trustworthy rather than
merely close:

```csharp
var gross = RoundMoney(high);
guard = 0;
while (NetAt(gross) < target && guard++ < 1000)          // climb until the guarantee holds
    gross += 0.01m;
guard = 0;
while (gross - 0.01m >= target && NetAt(gross - 0.01m) >= target && guard++ < 1000)
    gross -= 0.01m;                                       // trim to the smallest sufficient gross
```

The contract is deliberately one-sided: **net ≥ target**, never less. Rounding to the cent can
only ever overshoot, and the trim loop then reports the *smallest* gross that still satisfies the
promise. `Converged` records whether the guarantee was met at all — false only in pathological
cases, such as fixed W-4 extra withholding larger than any reachable net.

Every loop carries an iteration guard, so a hypothetical non-monotonic calculator would produce a
wrong answer rather than hang.

### How the probe is constructed

```csharp
private PaycheckResult CalculateAt(PaycheckInput input, decimal gross)
{
    var probe = new PaycheckInput
    {
        Frequency = input.Frequency,
        PayType = PayType.Salary,
        SalaryBasis = SalaryBasis.PerPeriod,
        SalaryAmount = gross,
        State = input.State,
        StateInputValues = input.StateInputValues,
        FederalW4 = input.FederalW4,
        Deductions = input.Deductions,
        YtdSocialSecurityWages = input.YtdSocialSecurityWages,
        YtdMedicareWages = input.YtdMedicareWages,
        PaycheckNumber = input.PaycheckNumber
    };
    return _payCalculator.Calculate(probe);
}
```

The template input supplies all tax context; its **gross-basis fields are ignored** — `PayType`,
hourly rate/hours, and salary amount are replaced by a per-period salary equal to the probe.

Two consequences to be aware of:

- The template's own `TaxYear` is **not** copied onto the probe, so probes always use
  `TaxYearSupport.Default`. With a single supported year this is a distinction without a
  difference; it would need attention when a second year is added.
- `targetNetPay` must be ≥ 0 (`ArgumentOutOfRangeException` otherwise), and a target of exactly
  zero short-circuits to a zero gross without searching.

### Result

`GrossUpResult` carries the target, the solved `GrossUpPay`, the full `PaycheckResult` computed
at that gross, and computed `ActualNetPay` / `GrossUpCost` properties. The UI shows a complete
itemized paycheck, not just a headline number.

---

## `BonusCalculator`

Supplemental wages — bonuses, commissions, awards, severance — are withheld differently from
regular pay. This is a **separate, non-annualized pipeline**: no W-4 percentage tables, no
annualize/de-annualize round trip.

It composes three collaborators and, like `PayCalculator`, must not absorb them:

```csharp
// 1) Federal flat supplemental withholding (22%; 37% above the cumulative $1M)
var (federal, federalExplanation) = _federal.CalculateWithExplanation(bonus, input.YtdSupplementalWages);

// 2) FICA — the bonus is fully FICA-taxable; YTD wages honor the SS cap and Additional Medicare threshold
var ficaDetail = _fica.CalculateWithExplanation(bonus, input.YtdSocialSecurityWages, input.YtdMedicareWages);

// 3) State supplemental withholding
var stateResult = _state.Calculate(input.State, bonus, RoundMoney(federal));
```

The same rounding contract as the standard pipeline applies — each component is rounded
individually, then net is derived from the rounded parts:

```csharp
var net = bonus - federalR - ssR - medicareR - addlR - stateR;
```

Note the bonus is treated as **fully FICA-taxable with no pre-tax deductions** — a supplemental
payment does not normally carry benefit deductions.

### State supplemental withholding

`Core/Tax/Supplemental/StateSupplementalCalculator.cs`, driven entirely by
`state_supplemental_2026.json`. Every state resolves to one of four methods:

```csharp
public enum StateSupplementalMethod
{
    NoIncomeTax,        // nothing withheld
    FlatRate,           // Rate × payment
    FederalPercentage,  // Rate × federal supplemental withholding (Vermont: 30%)
    RegularMethod       // no published flat rate — not estimated
}
```

`FederalPercentage` is why the federal number is passed into the state step. A state with no
entry in the table falls back to `RegularMethod`.

`RegularMethod` is handled honestly rather than guessed:

```csharp
if (stateResult.UsesRegularMethod)
{
    accuracyNotes.Add(new AccuracyNote(
        "Unsupported state method",
        $"{input.State} requires the regular/aggregate supplemental method. State withholding is omitted, so net bonus is before state income tax."));
}
```

`BonusResult.StateUsesRegularMethod` lets the UI render a visible caveat next to the net figure.
Per the manifest, 21 jurisdictions currently fall into this category
(`UnsupportedRegularAggregateSupplementalMethod`).

### What bonus mode excludes

Always attached as an `AccuracyNote`:

> Local taxes and state disability or paid-leave payroll assessments are not included in bonus mode.

So no CA SDI, no CO FAMLI, no CT paid leave, no WA Cares on a bonus run.

Source attribution uses `TaxCalculationMode.Bonus` with scopes `SupplementalWithholding` (federal
and state), `SocialSecurityMedicare`, and `AdditionalMedicare`.

---

## `SelfEmploymentCalculator`

An **annual, non-projected** pipeline for 1099 contractors.

```csharp
public const decimal SocialSecurityRate     = 0.124m;   // both halves
public const decimal MedicareRate           = 0.029m;   // both halves
public const decimal AdditionalMedicareRate = 0.009m;
public const decimal NetEarningsMultiplier  = 0.9235m;
```

The self-employed pay *both* the employer and employee halves of FICA — 12.4% + 2.9% = the
familiar 15.3%. The 92.35% multiplier is the statutory stand-in for the employer half a business
could deduct.

The wage base and threshold are **not** redeclared here:

```csharp
public SelfEmploymentCalculator(StateCalculatorRegistry stateRegistry, FicaCalculator fica, TaxSourceCatalog? sourceCatalog = null)
{
    // Share the FICA wage base / Additional-Medicare threshold so SE tax stays in sync.
    _socialSecurityWageBase      = fica.SocialSecurityWageBase;
    _additionalMedicareThreshold = fica.AdditionalMedicareEmployerThreshold;
}
```

One place to change the wage base; both pipelines follow.

### The computation

```csharp
var seBase = earnings * NetEarningsMultiplier;                       // 1) 92.35% base

var remainingSsBase = Math.Max(0m, _socialSecurityWageBase - input.YtdSocialSecurityWages);
var ssTaxable = Math.Min(seBase, remainingSsBase);
var ss = ssTaxable * SocialSecurityRate;                             // 2) capped 12.4%

var medicare = seBase * MedicareRate;                                // 3) uncapped 2.9%

var prior = input.YtdMedicareWages;                                  // 4) same threshold-crossing
var current = prior + seBase;                                        //    form as FicaCalculator
var over = Math.Max(0m, current - _additionalMedicareThreshold)
         - Math.Max(0m, prior   - _additionalMedicareThreshold);
var addl = over * AdditionalMedicareRate;

var seTax = RoundMoney(ss) + RoundMoney(medicare) + RoundMoney(addl);
```

YTD wage fields matter here for a real reason: a contractor with a W-2 day job has already used
part of the Social Security wage base, and part of the $200,000 Medicare runway.

### The state estimate

There is **no separate state self-employment tax anywhere in the US** — states tax
self-employment income as ordinary income. So the state figure is produced by running the full
net earnings through the ordinary state engine at annual frequency:

```csharp
var stateContext = new CommonWithholdingContext(
    input.State,
    GrossWages: earnings,
    PayPeriod: PayFrequency.Annual,
    Year: input.TaxYear,
    PreTaxDeductionsReducingStateWages: 0m,
    FederalWithholdingPerPeriod: 0m);
var stateResult = stateCalc.Calculate(stateContext, stateValues);
```

This is a **wage-withholding formula used as an income-tax proxy**, and the result says so in an
`AccuracyNote`. State disability and paid-leave levies are wage-based payroll taxes and are not
applied to self-employment income.

### Quarterly estimated payments

```csharp
private static (decimal[] amounts, decimal perQuarter) SplitIntoQuarters(decimal annual)
{
    var perQuarter = RoundMoney(annual / 4m);
    var amounts = new[] { perQuarter, perQuarter, perQuarter, RoundMoney(annual - perQuarter * 3m) };
    return (amounts, perQuarter);
}
```

The fourth installment absorbs the rounding remainder so the four sum **exactly** to the annual
figure. Federal and state amounts are split independently, then zipped with the standard
Form 1040-ES schedule:

| Quarter | Income period | Due |
|---|---|---|
| Q1 | Jan 1 – Mar 31, 2026 | 2026-04-15 |
| Q2 | Apr 1 – May 31, 2026 | 2026-06-15 |
| Q3 | Jun 1 – Aug 31, 2026 | 2026-09-15 |
| Q4 | Sep 1 – Dec 31, 2026 | 2027-01-15 |

Note the uneven income periods — that is the real IRS schedule, not a bug. Q4 is due in the
*following* calendar year.

### What self-employment mode does not do

Four `AccuracyNote`s ship with every result:

1. **Federal income tax is excluded.** SE tax is computed; income tax depends on filing status
   and other income and is settled at filing.
2. **State is a proxy** — the wage-withholding formula applied to annual net earnings.
3. **Payroll assessments excluded** — state disability/paid-leave and filing-time adjustments.
4. **Installment dates are the federal ones**; state schedules can differ.

The explanation reuses `ExplanationLineKey.FederalWithholding` for the "Self-Employment Tax"
total, with an inline comment explaining why: SE tax *is* the federal tax on these earnings, and
this mode has no income-tax withholding row to conflict with.

---

## `AnnualProjectionCalculator`

Extends a single per-period result into a full-year view. Shown on the **Annual** results tab in
both front-ends.

```csharp
public AnnualProjection Calculate(PaycheckInput input, PaycheckResult result)
```

### Three groups of numbers

**Annualized** — per-period × periods per year, for gross, pre/post-tax deductions, all three
taxable bases, federal, state, FICA, and net.

**Projected YTD** — per-period × current paycheck number:

```csharp
int paycheckNum = Math.Clamp(input.PaycheckNumber, 1, periods);
int remaining   = periods - paycheckNum;
```

The clamp keeps a nonsensical paycheck number (say 40 on a monthly schedule) from producing
absurd projections.

**Over/under withholding** — the interesting part:

```csharp
// Federal: re-run the IRS calculator at Annual frequency with Step 4(c) zeroed,
// so we get the base liability without voluntary extra withholding.
var liabilityW4 = new FederalW4Input { /* copy of input.FederalW4 */ Step4cExtraWithholding = 0m };
decimal estimatedFedLiability = _fed.CalculateWithholding(annualFedTaxable, PayFrequency.Annual, liabilityW4);

decimal estimatedFicaLiability   = EstimateFicaLiability(annualFicaTaxable);
decimal estimatedStateLiability  = annualStateWithholding;   // annualized withholding as proxy

decimal overUnder = R(annualTotalWithholding - estimatedTotal);
```

Zeroing Step 4(c) is the key move: voluntary extra withholding should show up as *over*
withholding (a bigger refund), not be baked into the liability it is being compared against.

FICA liability is computed directly against annualized wages with the caps applied once:

```csharp
decimal ss          = Math.Min(annualFicaWages, _fica.SocialSecurityWageBase) * FicaCalculator.SocialSecurityRate;
decimal medicare    = annualFicaWages * FicaCalculator.MedicareRate;
decimal addlMedicare = Math.Max(0m, annualFicaWages - _fica.AdditionalMedicareEmployerThreshold) * FicaCalculator.AdditionalMedicareRate;
```

Sign convention: **positive `OverUnderWithholding` = over-withholding (likely refund); negative =
under-withholding (likely owe).**

### What the projection is not

Five `AccuracyNote`s, and they are the honest framing of the feature:

1. The current paycheck is assumed to repeat unchanged for every remaining period.
2. Pub 15-T withholding is a **proxy**; this is not a Form 1040 liability calculation.
3. Annualized state withholding is a **proxy** for state income-tax liability.
4. Filing-time deductions, credits, other jobs, local taxes, and jurisdiction-specific
   reconciliation are not fully modeled.
5. The employer's $200,000 Additional Medicare threshold is used and may differ from final
   filing liability.

There is deliberately **no Form 1040 planner** in this product. The Annual tab is a withholding
projection.

---

## `HourlySalaryCalculator`

A pure rate conversion — no taxes, no deductions, no W-4. It answers "what salary is this hourly
rate worth?" and, more usefully, "what is my *real* hourly rate?"

Annual salary is the pivot:

```csharp
var hoursPerYear = input.HoursPerWeek * input.WeeksPerYear;

if (input.Mode == PayConversionMode.HourlyToSalary)
{
    hourly = input.HourlyRate;
    annual = hourly * hoursPerYear;
}
else
{
    annual = input.AnnualSalary;
    hourly = annual / hoursPerYear;
}
```

`HoursPerWeek` defaults to 40 and `WeeksPerYear` to 52, but both are inputs precisely so a user
can enter the hours they *actually* work (50, say) or paid weeks after unpaid leave (50) and see
the true rate behind a salary.

Per-frequency amounts all derive from the **unrounded** annual figure via `PayPeriods.PerYear`, so
the frequency counts match the rest of the engine:

```csharp
private static decimal PerPeriod(decimal annual, PayFrequency frequency) =>
    RoundMoney(annual / PayPeriods.PerYear(frequency));
```

Weekly, biweekly, semimonthly, and monthly equivalents are always reported; `PerPeriodPay`
reports the caller's chosen `Frequency`. Because each is rounded independently from the unrounded
annual, they need not sum back to the annual exactly — documented on the result type.

Validation throws rather than returning an error list: `HoursPerWeek` and `WeeksPerYear` must be
> 0, and the source rate/salary must be ≥ 0.

---

## Mode comparison

| | Standard | Gross-up | Bonus | Self-employment |
|---|---|---|---|---|
| Period basis | one pay period | one pay period | one payment | one year |
| Federal method | Pub 15-T percentage | Pub 15-T percentage | Pub 15 §7 flat rate | SE tax (no income tax) |
| Annualized? | yes, internally | yes, internally | no | n/a |
| FICA | employee half | employee half | employee half | **both halves** on 92.35% |
| Pre-tax deductions | yes | yes | no | no |
| State method | full withholding engine | full withholding engine | supplemental table | withholding engine as proxy |
| Disability / paid leave | yes | yes | **no** | **no** |
| Annual projection | yes | via its `PaycheckResult` | no | n/a (already annual) |

---

## Tests

| Test file | Covers |
|---|---|
| `GrossUpCalculatorTest` | Convergence, the net ≥ target guarantee, percentage deductions, FICA caps, zero target |
| `BonusCalculatorTest` | Flat-rate split, FICA on bonuses, all four state methods, the regular-method caveat |
| `SelfEmploymentCalculatorTest` | 92.35% base, wage-base cap, Additional Medicare crossing, quarterly split summing exactly |
| `AnnualProjectionCalculatorTest` | Annualization, YTD projection, paycheck-number clamping, over/under sign |
| `HourlySalaryCalculatorTest` | Both directions, custom hours/weeks, per-frequency amounts |
| `HourlySalaryValidationTest` | `PaycheckInputValidator.ValidateHourlySalary` — non-positive hours/weeks, negative rate/salary, the unused direction being ignored |
| `StateSupplementalCalculatorTest` | Table-driven method resolution, Vermont's federal-percentage rule |
| `FederalSupplementalCalculatorTest` | The $1M cumulative threshold split |

---

**Next:** [07 — Explanations & Source Governance](07-explanations-and-source-governance.md)
