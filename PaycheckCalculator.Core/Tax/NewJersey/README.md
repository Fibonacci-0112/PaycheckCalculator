# New Jersey (NJ)

New Jersey state income tax withholding is implemented by the dedicated
`NewJerseyWithholdingCalculator` in this folder.

## Formula

The calculator uses the annualized percentage-method formula from the New Jersey
Division of Taxation publication **NJ-WT** (Employer's Guide to New Jersey Gross
Income Tax Withholding) and Form **NJ-W4** (Employee's Withholding Allowance
Certificate).

**Calculation steps:**
1. Per-period state taxable wages = max(0, gross wages − pre-tax deductions).
2. Annualize wages (× pay periods per year).
3. Subtract per-allowance deduction ($1,000 × NJ-W4 allowances) to get annual
   taxable income. New Jersey has **no standard deduction**.
4. Floor annual taxable income at zero.
5. Apply the 2026 NJ graduated income tax brackets (Table A or B, determined
   by filing status).
6. De-annualize (÷ pay periods per year) and round to two decimal places.
7. Add any additional per-period withholding requested on Form NJ-W4.

## Filing statuses

| NJ-W4 Status | Description | Bracket table |
|---|---|---|
| A | Single | Table A (single) |
| B | Married/Civil Union Filing Jointly | Table B (married) |
| C | Married/Civil Union Filing Separately | Table A (single) |
| D | Head of Household / Qualifying Widow(er) | Table B (married) |
| E | Surviving Civil Union Partner | Table B (married) |

## 2026 brackets

**Table A — Single and Married Filing Separately (Status A and C):**
| Annual taxable income | Rate |
|---|---|
| $0 – $20,000 | 1.40% |
| $20,000 – $35,000 | 1.75% |
| $35,000 – $40,000 | 3.50% |
| $40,000 – $75,000 | 5.53% |
| $75,000 – $500,000 | 6.37% |
| $500,000 – $1,000,000 | 8.97% |
| Over $1,000,000 | 10.75% |

**Table B — Married, Head of Household, Surviving Partner (Status B, D, and E):**
| Annual taxable income | Rate |
|---|---|
| $0 – $20,000 | 1.40% |
| $20,000 – $50,000 | 1.75% |
| $50,000 – $70,000 | 2.45% |
| $70,000 – $80,000 | 3.50% |
| $80,000 – $150,000 | 5.53% |
| $150,000 – $500,000 | 6.37% |
| $500,000 – $1,000,000 | 8.97% |
| Over $1,000,000 | 10.75% |

## Sources
- New Jersey Division of Taxation, NJ-WT (Employer's Guide to New Jersey Gross
  Income Tax Withholding), 2026.
- Form NJ-W4, Employee's Withholding Allowance Certificate.

