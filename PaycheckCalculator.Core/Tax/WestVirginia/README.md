# West Virginia (WV)

West Virginia state income tax withholding is implemented by the dedicated
`WestVirginiaWithholdingCalculator` in this folder.

## Form and filing statuses

West Virginia uses **Form IT-104** (Employee's Withholding Exemption Certificate).
The two filing statuses are:

- **Single** — single filers
- **Married** — married filers

West Virginia does not define a separate Head of Household withholding status.

## Withholding formula (2026)

1. Compute per-period state taxable wages (`gross − pre-tax deductions`, floored at `$0`).
2. Annualize wages (`taxable wages × pay periods per year`).
3. Subtract personal exemptions (`exemptions × $2,000`).  
   West Virginia has **no state standard deduction** in the withholding formula.
4. Floor annual taxable income at `$0` (low-income exemption).
5. Apply the graduated brackets to annual taxable income.
6. De-annualize (`annual tax ÷ pay periods per year`) and round to two decimal places.
7. Add any per-period additional withholding from Form IT-104.

## 2026 brackets (all filing statuses share the same thresholds)

| Annual taxable income | Rate  |
|-----------------------|-------|
| $0 – $10,000          | 3.00% |
| $10,001 – $25,000     | 4.00% |
| $25,001 – $40,000     | 4.50% |
| $40,001 – $60,000     | 6.00% |
| Over $60,000          | 6.50% |

## Sources

- West Virginia State Tax Department, *Employee's Withholding Exemption Certificate* (Form IT-104), effective 2026.
- West Virginia Code § 11-21-71 (income tax brackets and rates).

