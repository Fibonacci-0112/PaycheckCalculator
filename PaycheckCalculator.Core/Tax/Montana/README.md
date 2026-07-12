# Montana (MT)

Montana state income tax withholding is computed by the dedicated
`MontanaWithholdingCalculator` in this folder, which implements the
annualized percentage-method formula from the Montana Department of Revenue
Withholding Tax Guide and Form MW-4.

## Key parameters (2026)

| Parameter | Single / MFS | Married / HoH |
|---|---|---|
| Standard deduction | 20% of wages, min $4,370, max $5,310 | 20% of wages, min $8,740, max $10,620 |
| Per-exemption deduction (MW-4) | $3,040 | $3,040 |

## Tax brackets (all filing statuses)

| Taxable income | Rate |
|---|---|
| $0 – $23,800 | 4.7% |
| Over $23,800 | 5.9% |

## Sources

- Montana Department of Revenue, *Montana Withholding Tax Guide*, 2026.
- Form MW-4, *Employee's Withholding Allowance and Exemption Certificate*.
- Montana HB 192 (2021) and SB 399 (2023): rate reductions to current levels.
