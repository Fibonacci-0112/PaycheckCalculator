# Louisiana (LA)

Louisiana state income tax withholding is computed by the dedicated
`LouisianaWithholdingCalculator`, located in this folder.

## Withholding method

The calculator implements the annualized percentage method described in Louisiana
Department of Revenue Publication R-1306 (Withholding Tables and Formulas) and
Form L-4 (Employee's Withholding Exemption Certificate).

### Filing statuses (L-4)

| L-4 status         | Personal exemption | Brackets used |
|--------------------|--------------------|---------------|
| Single             | $4,500             | Single        |
| Married            | $9,000             | Married       |
| Head of Household  | $9,000             | Married       |

### Per-dependent deduction

$1,000 per dependent claimed on L-4 Line 6B.

### 2026 graduated brackets

**Single**

| Annual taxable income | Rate  |
|-----------------------|-------|
| $0 – $12,500          | 1.85% |
| $12,501 – $50,000     | 3.50% |
| Over $50,000          | 4.25% |

**Married / Head of Household**

| Annual taxable income  | Rate  |
|------------------------|-------|
| $0 – $25,000           | 1.85% |
| $25,001 – $100,000     | 3.50% |
| Over $100,000          | 4.25% |

### Calculation steps

1. Compute per-period taxable wages (gross wages minus pre-tax deductions that reduce state wages).
2. Annualize wages (multiply by pay periods per year).
3. Subtract the personal exemption for the filing status.
4. Subtract the dependent deduction ($1,000 × number of dependents).
5. Apply the graduated brackets to the annual taxable income.
6. De-annualize (divide by pay periods per year) and round to two decimal places (half-away-from-zero).
7. Add any additional per-period withholding requested on L-4 Line 7.

## Source

- Louisiana Department of Revenue, R-1306 (Withholding Tables and Formulas)
- Form L-4, Employee's Withholding Exemption Certificate
