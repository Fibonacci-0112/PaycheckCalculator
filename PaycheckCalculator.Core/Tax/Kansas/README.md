# Kansas (KS)

Kansas state income tax withholding is computed by `KansasWithholdingCalculator`
using the K-4 annualized percentage method.

## 2026 formula summary

| Parameter | Single | Married |
|-----------|--------|---------|
| Standard deduction | $3,605 | $8,240 |
| Allowance (K-4) | $2,250 each | $2,250 each |
| Lower bracket | 5.20% on first $23,000 | 5.20% on first $46,000 |
| Upper bracket | 5.58% above $23,000 | 5.58% above $46,000 |

## Calculation steps

1. State taxable wages = gross wages − pre-tax deductions (floored at $0).
2. Annualize by multiplying by pay periods per year.
3. Subtract the standard deduction for the filing status.
4. Subtract K-4 allowances × $2,250.
5. Floor at $0.
6. Apply the two-bracket graduated schedule.
7. De-annualize (÷ pay periods) and round to two decimal places.
8. Add any extra per-period withholding from K-4.

## Sources

- Kansas Department of Revenue — Kansas Withholding Tax Guide (2026).
- Kansas Form K-4 "Kansas Employee's Withholding Allowance Certificate".

