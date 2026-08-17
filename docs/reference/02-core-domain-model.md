# 02 — Core Domain Model

Every type the calculation engine takes in and hands back. These live in
`PaycheckCalculator.Core/Models/`, with two close relatives in `Core/Pay/` and
`Core/Validation/`.

Design notes that apply throughout:

- **All money is `decimal`.** Never `double` or `float`. Binary floating point cannot represent
  cents exactly, and tax law is specified in cents.
- **Inputs and results are immutable.** Properties use `init` accessors; collections are
  exposed as `IReadOnlyList<T>` and default to `Array.Empty<T>()`.
- **Defaults are meaningful.** `OvertimeMultiplier` defaults to `1.5m`, `TaxYear` to
  `TaxYearSupport.Default`, `State` to `UsState.OK`, so a minimally-populated input still
  produces a sensible calculation.

---

## `PaycheckInput`

The single input to the standard pipeline (`Core/Models/PaycheckInput.cs`).

```csharp
public sealed class PaycheckInput
{
    public PayFrequency Frequency { get; init; }
    public PayType PayType { get; init; } = PayType.Hourly;

    // Hourly branch
    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    // Salary branch
    public decimal SalaryAmount { get; init; }
    public SalaryBasis SalaryBasis { get; init; } = SalaryBasis.PerYear;

    public UsState State { get; init; } = UsState.OK;
    public StateInputValues? StateInputValues { get; init; }
    public FederalW4Input FederalW4 { get; init; } = new();
    public IReadOnlyList<Deduction> Deductions { get; init; } = Array.Empty<Deduction>();

    public decimal YtdSocialSecurityWages { get; init; } = 0m;
    public decimal YtdMedicareWages { get; init; } = 0m;

    public int PaycheckNumber { get; init; } = 1;
    public int TaxYear { get; init; } = TaxYearSupport.Default;
}
```

Field-by-field:

| Field | Notes |
|---|---|
| `Frequency` | Drives annualization everywhere. See [`PayPeriods`](#payperiods) below. |
| `PayType` | Selects the hourly or salary branch of gross-pay computation. The unused branch's fields are ignored, not validated. |
| `OvertimeMultiplier` | Defaults to time-and-a-half. Validation requires ≥ 1.0. |
| `SalaryBasis` | `PerYear` divides by pay periods; `PerPeriod` uses the amount as-is. |
| `StateInputValues` | Nullable. `PayCalculator` substitutes an empty bag when null, so every state calculator can rely on defaults. |
| `FederalW4` | Never null — defaults to a fresh `FederalW4Input` (Single/MFS, no adjustments). |
| `YtdSocialSecurityWages` | Prior-period SS wages, so the wage-base cap is honored mid-year. |
| `YtdMedicareWages` | Prior-period Medicare wages, so the $200,000 Additional Medicare threshold is crossed only once. |
| `PaycheckNumber` | 1-based within the year. Only consumed by `AnnualProjectionCalculator`. |
| `TaxYear` | Persisted with saved snapshots so a stored paycheck is never silently recalculated under newer tables. |

---

## `PaycheckResult`

What the pipeline returns (`Core/Models/PaycheckResult.cs`). Every monetary property is already
rounded to the cent by `PayCalculator`.

```csharp
public sealed class PaycheckResult
{
    public decimal GrossPay { get; init; }
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }
    public int TaxYear { get; init; } = TaxYearSupport.Default;

    public UsState State { get; init; }
    public decimal StateTaxableWages { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public decimal FicaTaxableWages { get; init; }
    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    public decimal FederalTaxableIncome { get; init; }
    public decimal FederalWithholding { get; init; }

    public decimal TotalTaxes => StateWithholding + StateDisabilityInsurance
                               + SocialSecurityWithholding + MedicareWithholding
                               + AdditionalMedicareWithholding + FederalWithholding;
    public decimal NetPay { get; init; }

    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}
```

**Three separate taxable-wage bases are reported, and they genuinely differ.**
`FederalTaxableIncome`, `FicaTaxableWages`, and `StateTaxableWages` each start from gross and
subtract a *different* subset of pre-tax deductions. This is not redundancy — it is the reason
`Deduction` carries three independent flags. See [Deduction](#deduction) below and
[03 — The Calculation Pipeline](03-calculation-pipeline.md).

`StateDisabilityInsuranceLabel` exists because the line means different things in different
states: "State Disability Insurance" in California, "Family Leave Insurance" in Connecticut,
"WA Cares Fund (Long-Term Care)" in Washington. The calculator supplies the label; the UI just
renders it.

`TotalTaxes` is computed, not stored — it always agrees with its parts.

---

## `Deduction`

The most subtle type in the model (`Core/Models/Deduction.cs`).

```csharp
public sealed class Deduction
{
    public string Name { get; init; } = "";
    public DeductionType Type { get; init; }              // PreTax | PostTax
    public decimal Amount { get; init; }
    public DeductionAmountType AmountType { get; init; } = DeductionAmountType.Dollar;

    public bool ReducesFederalTaxableWages { get; init; } = true;
    public bool ReducesStateTaxableWages { get; init; } = true;
    public bool ReducesFicaWages { get; init; } = true;

    public decimal EffectiveAmount(decimal grossPay) => AmountType switch
    {
        DeductionAmountType.Percentage => Amount / 100m * grossPay,
        _ => Amount
    };
}
```

### Why three flags

US payroll law treats pre-tax deductions differently for income tax and for FICA. The three
booleans encode that directly rather than hiding it behind a benefit-type enum:

| Real-world deduction | `ReducesFederalTaxableWages` | `ReducesStateTaxableWages` | `ReducesFicaWages` |
|---|:--:|:--:|:--:|
| Section 125 cafeteria plan (medical, dental, FSA, HSA) | ✅ | ✅ | ✅ |
| Traditional 401(k) / 403(b) / 457 | ✅ | ✅ | ❌ |
| Roth 401(k) / 403(b) | ❌ | ❌ | ❌ |
| Post-tax (garnishment, union dues, Roth IRA) | n/a — `Type = PostTax` | n/a | n/a |

The two rules that trip people up:

- **401(k) contributions reduce income-tax wages but are still subject to FICA.** You pay
  Social Security and Medicare on money you defer into a traditional 401(k). Hence
  `ReducesFicaWages = false` for retirement deferrals.
- **Roth contributions are after-tax money and reduce nothing.** They still appear as a
  `PreTax`-typed deduction in the sense that they are withheld before the check is cut, but all
  three flags are `false` so no tax base moves.

State conformity is not uniform, which is why `ReducesStateTaxableWages` is separate from the
federal flag rather than derived from it.

### `EffectiveAmount` and percentage deductions

`AmountType.Percentage` means "`Amount` percent of gross pay". Because it is evaluated against
gross at calculation time, a percentage deduction automatically scales when gross changes — which
is exactly what makes the gross-up solver correct without special-casing (see
[06 — Alternate Calculators](06-alternate-calculators.md)).

`PostTax` deductions never touch a tax base; they reduce net pay only.

---

## Enums

All in `Core/Models/Enums.cs` unless noted.

### `PayFrequency`

```csharp
public enum PayFrequency
{
    Weekly, Biweekly, Semimonthly, Monthly,
    Quarterly, Semiannual, Annual, Daily,
    Weekly53, Biweekly27
}
```

`Weekly53` and `Biweekly27` cover the calendar years that contain an extra pay period —
53 weekly or 27 biweekly checks. They matter because annualization multiplies by the period
count, and using 52/26 in a 53/27 year systematically over-withholds.

### The rest

| Enum | Values | Purpose |
|---|---|---|
| `FilingStatus` | `Single`, `Married` | The simplified two-way status used by the generic percentage-method state calculator. Distinct from `FederalFilingStatus`. |
| `DeductionType` | `PreTax`, `PostTax` | Whether the deduction can affect a tax base at all. |
| `DeductionAmountType` | `Dollar`, `Percentage` | How `Amount` is interpreted. |
| `PayType` | `Hourly`, `Salary` | Which gross-pay branch to use. |
| `SalaryBasis` | `PerYear`, `PerPeriod` | Whether `SalaryAmount` is annual or per-period. |
| `PayConversionMode` | `HourlyToSalary`, `SalaryToHourly` | Direction for `HourlySalaryCalculator`. |

`FederalFilingStatus` (`SingleOrMarriedSeparately`, `MarriedFilingJointly`, `HeadOfHousehold`)
lives in `Core/Tax/Federal/FederalModels.cs` — see
[04 — Federal Withholding & FICA](04-federal-and-fica.md).

---

## `UsState`

```csharp
public enum UsState
{
    AK, AL, AR, AZ, CA, CO, CT, DC, DE, FL,
    GA, HI, IA, ID, IL, IN, KS, KY, LA, MA,
    MD, ME, MI, MN, MO, MS, MT, NC, ND, NE,
    NH, NJ, NM, NV, NY, OH, OK, OR, PA, RI,
    SC, SD, TN, TX, UT, VA, VT, WA, WI, WV,
    WY
}
```

51 members: 50 states plus `DC`, using USPS two-letter abbreviations in alphabetical order.

Two consequences worth internalizing:

- **The member name is load-bearing.** `AddPaycheckCalculatorCore` lowercases each enum name to
  find `schemas/<name>.json`, and `TaxSourceCatalog` parses manifest `jurisdiction` strings back
  into `UsState`. Renaming a member breaks data loading at startup.
- **Ordinal values are never persisted.** `PaycheckJson` registers a
  `JsonStringEnumConverter` precisely so reordering the enum cannot corrupt stored snapshots.

---

## `TaxYearSupport`

```csharp
public static class TaxYearSupport
{
    public const int CurrentTaxYear = 2026;
    public const int Default = CurrentTaxYear;
    public static IReadOnlyList<int> SupportedTaxYears { get; } = new[] { CurrentTaxYear };
    public static bool IsSupported(int year) => year == CurrentTaxYear;
}
```

This build ships exactly one year of tables. Every entry-point calculator gates on it:

```csharp
if (!TaxYearSupport.IsSupported(input.TaxYear))
    throw new NotSupportedException(
        $"Tax year {input.TaxYear} is not supported. Only {TaxYearSupport.Default} tax data is currently loaded.");
```

`PayCalculator`, `BonusCalculator`, and `SelfEmploymentCalculator` all perform this check.
`TaxSourceCatalog.Load` enforces the same constraint on the manifest and on every rule inside it,
so a mismatched data file fails fast at startup rather than producing wrong numbers at runtime.

`TaxYear` is carried on inputs *and* echoed onto results and saved snapshots. That is what lets
a stored 2026 paycheck be recognized as a 2026 paycheck after a future year is added, rather
than being silently re-run against new tables.

---

## Result types for the alternate calculators

Each alternate pipeline has its own input/result pair. They are covered in detail in
[06 — Alternate Calculators](06-alternate-calculators.md); this is the map.

| Input | Result | Pipeline |
|---|---|---|
| `PaycheckInput` + `decimal targetNetPay` | `GrossUpResult` | `GrossUpCalculator` |
| `BonusInput` | `BonusResult` | `BonusCalculator` |
| `SelfEmploymentInput` | `SelfEmploymentResult` (+ `QuarterlyEstimate` record) | `SelfEmploymentCalculator` |
| `PaycheckInput` + `PaycheckResult` | `AnnualProjection` | `AnnualProjectionCalculator` |
| `HourlySalaryInput` | `HourlySalaryResult` | `HourlySalaryCalculator` |

Common traits: `BonusResult`, `SelfEmploymentResult`, and `AnnualProjection` each carry an
`IReadOnlyList<AccuracyNote> AccuracyNotes` describing what the mode does *not* model, and
`BonusResult`/`SelfEmploymentResult` carry a full `PaycheckExplanation` just like
`PaycheckResult`.

`GrossUpResult` is worth a second look because it is mostly computed:

```csharp
public decimal ActualNetPay => Paycheck.NetPay;
public decimal GrossUpCost  => GrossUpPay - TargetNetPay;
public bool Converged { get; init; }
```

It embeds the full `PaycheckResult` computed at the solved gross, so the UI shows a real
itemized paycheck rather than a summary.

---

## `PayPeriods`

`Core/Pay/PayPeriods.cs` — the canonical frequency → periods-per-year map.

```csharp
public static int PerYear(PayFrequency frequency) => frequency switch
{
    PayFrequency.Weekly       => 52,
    PayFrequency.Biweekly     => 26,
    PayFrequency.Semimonthly  => 24,
    PayFrequency.Monthly      => 12,
    PayFrequency.Quarterly    => 4,
    PayFrequency.Semiannual   => 2,
    PayFrequency.Annual       => 1,
    PayFrequency.Daily        => 260,
    PayFrequency.Weekly53     => 53,
    PayFrequency.Biweekly27   => 27,
    _ => throw new ArgumentOutOfRangeException(...)
};
```

`Daily = 260` is the standard payroll working-days convention (52 × 5), not 365.

**A caveat that matters when you touch this area.** Three other places contain their own copy of
this mapping:

| Location | Coverage | Behavior on `Weekly53`/`Biweekly27` |
|---|---|---|
| `PayPeriods.PerYear` | All 10 frequencies | 53 / 27 |
| `Irs15TPercentageCalculator.PayPeriodsPerYear` | All 10 (returns `decimal`) | 53 / 27 |
| `AnnualProjectionCalculator.PayPeriodsPerYear` | All 10 | 53 / 27 |
| `PercentageMethodStateTaxCalculator.GetPayPeriods` | 8 only | **throws `ArgumentOutOfRangeException`** |

The generic percentage-method state calculator predates the extra-period frequencies. It is not
wired to any production state today (see
[05 — The State Withholding Engine](05-state-withholding-engine.md#4-the-generic-percentage-method-calculator)),
so the gap is currently unreachable in production — but it is a live trap if that adapter is
ever re-wired. `RecurrencePeriods.PerYear` in `Core/Budgeting/` is a deliberately separate map
over `RecurrenceFrequency`, not a duplicate.

---

## `PaycheckInputValidator`

`Core/Validation/PaycheckInputValidator.cs` — shared sanity checks so both front-ends surface the
same messages instead of inventing ad hoc validation.

```csharp
public const decimal MaxHoursPerPeriod = 744m;  // 31 days × 24 hours
public const int MaxPaycheckNumber = 366;       // supports daily frequency
```

It explicitly **does not encode tax law**. It catches "this input cannot possibly be right"
before a bad value produces a silently-wrong or crashing calculation. Three entry points, each
returning `IReadOnlyList<string>` (empty means valid):

### `Validate(PaycheckInput input, decimal? targetNetPay = null)`

Hourly branch (only when `PayType == Hourly`):
- Hourly rate, regular hours, overtime hours each non-negative
- `RegularHours + OvertimeHours ≤ 744`
- `OvertimeMultiplier ≥ 1.0`

Salary branch: `SalaryAmount ≥ 0`.

Always: `YtdSocialSecurityWages ≥ 0`, `YtdMedicareWages ≥ 0`,
`1 ≤ PaycheckNumber ≤ 366`. When `targetNetPay` is supplied (gross-up mode), it must be `> 0`.

### `ValidateBonus(BonusInput input)`

`BonusAmount > 0`; `YtdSupplementalWages`, `YtdSocialSecurityWages`, `YtdMedicareWages` all
non-negative.

### `ValidateSelfEmployment(SelfEmploymentInput input)`

`AnnualNetEarnings ≥ 0` (zero is allowed — a contractor can have no net profit);
YTD wage fields non-negative.

State-specific validation is a **separate** concern handled by each
`IStateWithholdingCalculator.Validate(StateInputValues)`. Both front-ends run both and merge the
message lists.

---

**Next:** [03 — The Calculation Pipeline](03-calculation-pipeline.md)
