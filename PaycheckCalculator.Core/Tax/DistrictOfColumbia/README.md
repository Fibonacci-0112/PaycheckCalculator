# District of Columbia (DC)

District of Columbia income tax withholding is implemented by
`DistrictOfColumbiaWithholdingCalculator` in this folder. It follows the
annualized percentage method per the DC Office of Tax and Revenue
FR-230 Income Tax Withholding Instructions and Tables and exposes a
schema-driven form based on DC Form D-4.

Filing statuses (D-4):

- Single — standard deduction $15,000
- Married/Registered Domestic Partners Filing Jointly — standard deduction $30,000
- Married/Registered Domestic Partners Filing Separately — standard deduction $15,000
- Head of Household — standard deduction $15,000

Per-allowance exemption (D-4 line 2): $1,675 annually, subtracted from
annual taxable wages after the standard deduction.

2026 brackets (same for all filing statuses):

| Annual taxable income | Rate   |
| --------------------- | ------ |
| $0 – $10,000          | 4.00%  |
| $10,000 – $40,000     | 6.00%  |
| $40,000 – $60,000     | 6.50%  |
| $60,000 – $250,000    | 8.50%  |
| $250,000 – $500,000   | 9.25%  |
| $500,000 – $1,000,000 | 9.75%  |
| Over $1,000,000       | 10.75% |

Additional withholding from D-4 line 3 is added to the per-period
withholding after rounding to the nearest cent.
