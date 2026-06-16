# Tax Calculation Engine

This page describes the paycheck calculation pipeline implemented by `PayCalculator` and the related Core calculators.

---

## Calculation Pipeline

`PayCalculator.Calculate(PaycheckInput)` runs one paycheck through the following steps.

### Step 1: Gross Pay

Gross pay depends on `PayType`:

- **Hourly**
  ```text
  Gross Pay = (Regular Hours × Hourly Rate)
            + (Overtime Hours × Hourly Rate × Overtime Multiplier)
  ```
  The default overtime multiplier is 1.5.

- **Salary**
  - `SalaryBasis.PerYear` divides annual salary by the selected pay frequency's periods per year.
  - `SalaryBasis.PerPeriod` uses the entered salary amount as the current paycheck's gross pay.

### Step 2: Deductions

Deductions can be fixed dollar amounts or percentages of gross pay.

| Deduction type | Effect |
|---|---|
| Pre-tax | Reduces one or more taxable wage bases before tax calculation |
| Post-tax | Subtracted after taxes |

Pre-tax deductions carry independent flags:

- `ReducesFederalTaxableWages`
- `ReducesStateTaxableWages`
- `ReducesFicaWages`

This is intentional. A traditional 401(k) may reduce federal/state taxable wages but not FICA wages; a Section 125 cafeteria-plan benefit can reduce all three.

### Step 3: FICA Taxes

`FicaCalculator` computes:

| Component | Rule |
|---|---|
| Social Security | 6.2% up to the 2026 wage base |
| Medicare | 1.45% with no cap |
| Additional Medicare | 0.9% above the annual threshold |

FICA is calculated on FICA-taxable wages: gross pay minus only the pre-tax deductions that reduce FICA wages. YTD Social Security and Medicare wage inputs allow the calculator to handle wage-base and threshold crossings mid-year.

### Step 4: Federal Withholding

`Irs15TPercentageCalculator` implements the IRS Publication 15-T 2026 percentage method for automated payroll systems.

High-level flow:

1. Annualize current-period wages.
2. Apply W-4 Step 4(a) other income.
3. Apply the filing-status / Step 2 branch from the 15-T data.
4. Apply Step 4(b) deductions.
5. Apply the annual percentage-method bracket table.
6. Subtract Step 3 credits.
7. De-annualize back to the pay period.
8. Add Step 4(c) extra withholding.

Supported W-4 inputs:

- `FederalFilingStatus.SingleOrMarriedSeparately`
- `FederalFilingStatus.MarriedFilingJointly`
- `FederalFilingStatus.HeadOfHousehold`
- Step 2 checkbox
- Step 3 credits
- Step 4(a) other income
- Step 4(b) deductions
- Step 4(c) extra withholding

### Step 5: State Withholding

`StateCalculatorRegistry` maps the selected `UsState` to an `IStateWithholdingCalculator`.

Each state calculator receives a `CommonWithholdingContext` containing current-period wages, pay frequency, tax year, state-taxable pre-tax deduction information, and federal withholding where states need it.

Each state calculator returns a `StateWithholdingResult` containing:

- State taxable wages.
- State income tax withholding.
- Employee-paid disability / paid-leave premium, if applicable.
- Display label for the premium line.
- Explanation steps.

State-specific input fields come from `GetInputSchema()` and are populated by `StateInputValues`.

### Step 6: Net Pay

```text
Net Pay = Gross Pay
        − Pre-Tax Deductions
        − Post-Tax Deductions
        − Federal Withholding
        − Social Security Withholding
        − Medicare Withholding
        − Additional Medicare Withholding
        − State Withholding
        − State Disability / Paid-Leave Premiums
```

---

## Rounding

Money values use `decimal`.

The engine rounds gross pay, taxes, and deductions to cents using `MidpointRounding.AwayFromZero`. Net pay is computed so the displayed result ties out to the visible components: gross minus taxes and deductions equals net to the cent.

Some state calculators also apply state-specific intermediate rounding rules. Example: Oklahoma's OW-2 path uses whole-dollar rounding where required by its table method.

---

## Pay Frequencies

| Frequency | Periods/Year |
|---|---:|
| Daily | 260 |
| Weekly | 52 |
| Weekly53 | 53 |
| Biweekly | 26 |
| Biweekly27 | 27 |
| Semimonthly | 24 |
| Monthly | 12 |
| Quarterly | 4 |
| Semiannual | 2 |
| Annual | 1 |

The mapping lives in `Pay/PayPeriods.cs`. The 53-week and 27-biweekly variants support payroll calendar years with an extra pay period.

---

## Gross-Up

`GrossUpCalculator` solves the inverse problem: given a target net pay, find the gross pay required to produce that net.

It uses the full `PayCalculator` pipeline at every probe, rather than using a simplified tax rate estimate. That means the result honors:

- Federal graduated brackets.
- State-specific formulas.
- FICA wage-base caps and Additional Medicare thresholds.
- Pre-tax and post-tax deductions.
- Percentage-of-gross deductions.
- State disability / paid-leave premiums.

The returned `GrossUpResult` contains the target net, solved gross, gross-up cost, convergence status, and the full `PaycheckResult` at the solved gross.

---

## Annual Projection

`AnnualProjectionCalculator` converts a per-paycheck result into year-level estimates:

- Pay periods per year.
- Current paycheck number.
- Remaining paychecks.
- Annualized gross, taxable wages, taxes, deductions, and net pay.
- Projected YTD values through the current paycheck.
- Estimated annual federal/FICA liability.
- Projected over/under withholding.

Both front-ends surface annual projection data alongside the per-paycheck result.

---

## Explanation Model

`PaycheckResult` carries a `PaycheckExplanation`. Each important output line can expose a `LineExplanation` with a title, final amount, reference, and ordered calculation steps.

Both front-ends use these records for the **Show Your Work** experience. The explanation model belongs in Core so MAUI, Blazor, tests, and exports can all rely on the same calculation trace.
