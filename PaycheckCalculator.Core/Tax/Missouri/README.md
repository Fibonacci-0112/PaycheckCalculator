# Missouri (MO)

Missouri state income tax withholding is computed by the dedicated
`MissouriWithholdingCalculator` (see `MissouriWithholdingCalculator.cs`).

## Form MO W-4 calculation (2026)

| Parameter | Value |
|-----------|-------|
| Standard deduction — Single | $15,750 |
| Standard deduction — Married | $31,500 |
| Standard deduction — Head of Household | $23,625 |
| Per MO W-4 allowance deduction | $2,100 |

Missouri mirrors the 2026 federal standard deduction amounts and applies the
same eight graduated brackets to all filing statuses:

| Annual taxable income | Rate |
|-----------------------|------|
| $0 – $1,313           | 0%   |
| $1,313 – $2,626       | 2%   |
| $2,626 – $3,939       | 2.5% |
| $3,939 – $5,252       | 3%   |
| $5,252 – $6,565       | 3.5% |
| $6,565 – $7,878       | 4%   |
| $7,878 – $9,191       | 4.5% |
| Over $9,191           | 4.7% |

## Sources

- Missouri Department of Revenue, *Employer's Withholding Tax Guide*, 2026.
- Form MO W-4, Employee's Withholding Certificate.
- Missouri SB 3 (2022) and SB 5 (2023 Special Session): phase-down of top
  rate to 4.7% and consolidation of lower brackets.

