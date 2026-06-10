# Maryland (MD)

Maryland state income tax withholding is computed by the dedicated
`MarylandWithholdingCalculator` in this folder.

## Formula (2026 Comptroller of Maryland Employer Withholding Guide)

1. Annualize per-period taxable wages (gross minus pre-tax deductions, × pay periods).
2. Compute the **variable standard deduction**: 15% of annual wages, bounded by
   filing-status minimums and maximums:
   - Single / MFS:            minimum **$1,600**, maximum **$2,550**
   - Married / Head of Household: minimum **$3,200**, maximum **$5,100**
3. Subtract the standard deduction and MW507 **exemptions** ($3,200 each).
4. Apply the appropriate **ten-bracket graduated rate schedule** (2%–6.5%).
5. De-annualize, round to two decimal places, and add any extra per-period
   withholding elected by the employee.

## Filing statuses (MW507)

| Status            | Standard deduction limits | Rate schedule used |
|-------------------|--------------------------|-------------------|
| Single            | $1,600 – $2,550          | Single            |
| Married           | $3,200 – $5,100          | Married/HoH       |
| Head of Household | $3,200 – $5,100          | Married/HoH       |

## County surtax

Maryland also levies a county income tax (1.75%–3.20% of taxable wages, or
2.25% for non-residents). That component is out of scope for this module and
is not modeled by this calculator.

