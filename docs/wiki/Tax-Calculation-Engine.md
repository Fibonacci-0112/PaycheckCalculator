# Tax Calculation Engine

This page describes the paycheck calculation pipeline implemented in `PayCalculator`.

---

## Calculation Pipeline

`PayCalculator.Calculate(PaycheckInput)` runs the following steps in order:

### Step 1: Gross Pay

Gross pay depends on the **pay type**:

- **Hourly:**
  ```
  Gross Pay = (Regular Hours × Hourly Rate) + (Overtime Hours × Hourly Rate × OT Multiplier)
  ```
  The default overtime multiplier is 1.5×.
- **Salary:** an annual amount divided by the number of pay periods, or a per-period amount used directly
  (`SalaryBasis` = `PerYear` or `PerPeriod`).

### Step 2: Deductions

Deductions are categorized as **pre-tax** or **post-tax**:

- **Pre-tax deductions** reduce taxable wages before federal and state tax calculations (e.g., 401(k), health insurance).
- **Post-tax deductions** are subtracted after taxes (e.g., Roth 401(k), garnishments).

Each deduction can be a **dollar amount** or a **percentage** of gross pay (`DeductionAmountType`).

Pre-tax deductions carry three independent flags — `ReducesFederalTaxableWages`, `ReducesStateTaxableWages`,
and `ReducesFicaWages` — because different deductions reduce different wage bases. For example, a
traditional 401(k) reduces federal and state taxable wages but **not** FICA wages, whereas a Section 125
cafeteria-plan benefit reduces all three.

### Step 3: FICA Taxes

`FicaCalculator` computes three components:

| Component | Rate | Wage Base (2026) |
|---|---|---|
| Social Security | 6.2% | $184,500 cap (wages above this are exempt) |
| Medicare | 1.45% | No cap |
| Additional Medicare | 0.9% | Wages above $200,000 |

FICA is calculated on FICA-taxable wages (`Gross Pay` minus only the pre-tax deductions that carry
`ReducesFicaWages`). Year-to-date (YTD) Social Security and Medicare wages are tracked to correctly apply
the annual Social Security wage-base cap and the Additional Medicare threshold mid-year.

### Step 4: Federal Withholding

`Irs15TPercentageCalculator` implements the IRS Publication 15-T (2026) percentage method for automated payroll systems:

1. Wages are **annualized** (multiplied by the number of pay periods per year).
2. The **standard deduction** is subtracted based on filing status.
3. W-4 **Step 4(b) adjustments** (additional deductions) are applied.
4. **Graduated tax brackets** are applied to the adjusted annual wage.
5. W-4 **Step 3 credits** (dependents) are subtracted from the computed tax.
6. W-4 **Step 4(c) extra withholding** is added.
7. The result is **de-annualized** back to the pay period.

Supported W-4 inputs:
- Filing status (`FederalFilingStatus`: `SingleOrMarriedSeparately`, `MarriedFilingJointly`, `HeadOfHousehold` — Single and MFS are folded into a single enum value to match Pub 15-T withholding tables)
- Step 2 checkbox (two jobs / spouse works)
- Step 3 credits (child/dependent tax credits)
- Step 4(a) other income
- Step 4(b) deductions
- Step 4(c) extra withholding

### Step 5: State Withholding

The `StateCalculatorRegistry` looks up the `IStateWithholdingCalculator` for the selected state and delegates the calculation. Each state receives a `CommonWithholdingContext` containing:

- State identifier
- Gross wages
- Pay frequency
- Tax year (2026)
- Pre-tax deductions that reduce state taxable wages
- Federal withholding per period (used by states like Alabama that deduct federal tax)

The calculator returns a `StateWithholdingResult` with:
- Taxable wages
- State income tax withholding
- Disability insurance (if applicable, e.g., CA SDI, CT PFMLI)
- Display label for the disability line item

See [State Tax Coverage](State-Tax-Coverage.md) for details on each state's implementation.

### Step 6: Net Pay

```
Net Pay = Gross Pay − Pre-Tax Deductions − Post-Tax Deductions − Federal Tax
          − State Tax − State Disability Insurance − Social Security − Medicare
          − Additional Medicare
```

---

## Rounding

`PayCalculator` rounds gross pay, taxes, and deductions individually using `MidpointRounding.AwayFromZero` (round half away from zero). **Net pay is computed from the unrounded components and then rounded** so that the displayed net equals `gross − taxes − deductions` to the cent. Some state calculators apply additional internal rounding rules (e.g., Oklahoma uses whole-dollar rounding for intermediate values).

---

## Pay Frequencies

| Frequency | Periods/Year |
|---|---|
| Daily | 260 |
| Weekly | 52 |
| Biweekly | 26 |
| Semimonthly | 24 |
| Monthly | 12 |
| Quarterly | 4 |
| Semiannual | 2 |
| Annual | 1 |
| Weekly53 | 53 |
| Biweekly27 | 27 |

`Weekly53` and `Biweekly27` cover payroll calendars with an extra pay period in some years. The mapping
lives in `Pay/PayPeriods.cs`.

---

## Gross-Up

`GrossUpCalculator` runs the pipeline **in reverse**: given a target net (take-home) amount, it
binary-searches (bisection) for the gross pay that produces it, re-running the full `PayCalculator`
pipeline at every probe so graduated brackets, FICA wage-base caps, and percentage-of-gross deductions are
all honored. It returns a `GrossUpResult` (target net, required gross, the cost of taxes and deductions
covered, and the full per-period `PaycheckResult` at the solved gross). It is surfaced in both front-ends
via a calculation-mode toggle.

---

## Annual Projection

`AnnualProjectionCalculator` extends the per-period result into a full-year estimate:

- Multiplies the current period's taxes and net pay by the total number of periods.
- Tracks the current paycheck number and remaining periods.
- Estimates year-end over/under withholding relative to the projected annual tax liability.
