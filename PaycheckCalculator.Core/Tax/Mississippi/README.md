# Mississippi (MS)

Mississippi state income tax withholding is computed by the dedicated
`MississippiWithholdingCalculator` using Form 89-350 (Employee's Withholding
Exemption Certificate) and the Mississippi Department of Revenue Employer's
Withholding Tax Instructions (Pub. 89-105).

## 2026 Parameters

| Parameter | Single / MFS | Married (MFJ) | Head of Household |
|---|---|---|---|
| Standard deduction | $2,300 | $4,600 | $3,400 |
| Personal exemption | $6,000 | $12,000 | $9,500 |
| Dependent exemption | $1,500 each (Form 89-350 Line 6) | same | same |

### Tax brackets (all filing statuses)

| Annual taxable income | Rate |
|---|---|
| $0 – $10,000 | 0% |
| Over $10,000 | 4% |

The 0% threshold on the first $10,000 results from MS HB 531 (2022), which
eliminated the 4% bracket on $5,001–$10,000.  The top rate was phased down
from 5% to 4% by tax year 2026 per MS HB 1 (2023).

## Formula

1. Per-period taxable wages = gross wages − pre-tax deductions (floored at $0).
2. Annual wages = per-period wages × pay periods.
3. Annual taxable income = max(0, annual wages − standard deduction − personal
   exemption − (dependents × $1,500)).
4. Annual tax = max(0, annual taxable income − $10,000) × 4%.
5. Per-period withholding = round(annual tax ÷ pay periods, 2) + extra withholding.

## Sources

- Mississippi Department of Revenue, *Employer's Withholding Tax Instructions*,
  Publication 89-105 (current edition).
- Form 89-350, *Employee's Withholding Exemption Certificate*.
- MS HB 531 (2022 Regular Session): eliminated the 4% income bracket.
- MS HB 1 (2023 Regular Session): phase-down of top income tax rate to 4% by 2026.
