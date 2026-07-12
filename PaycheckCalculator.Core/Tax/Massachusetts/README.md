# Massachusetts (MA)

Massachusetts state income tax withholding is computed by the dedicated
`MassachusettsWithholdingCalculator`, which implements the annualized percentage
method as described in the Massachusetts Department of Revenue (DOR) Employer's
Tax Guide and Form M-4 (Employee's Withholding Exemption Certificate).

## Formula

1. Annualize per-period wages (gross wages × pay periods per year).
2. Subtract the personal exemption for the M-4 filing status.
3. Subtract additional deductions claimed on M-4:
   - **$1,000** per dependent
   - **$2,200** per qualifying blind individual (employee or spouse)
   - **$700** per individual age 65 or over
4. Apply the flat 5% rate on annual taxable income up to $1,000,000; apply 9%
   (5% + 4% surtax per Massachusetts Question 1 / Fair Share Amendment, 2022)
   on the excess over $1,000,000.
5. De-annualize (÷ pay periods) and round to two decimal places.
6. Add any per-period extra withholding requested on M-4 Line 4.

## Filing statuses and personal exemptions (2026)

| Filing Status       | Personal Exemption |
|---------------------|--------------------|
| Single              | $4,400             |
| Married             | $8,800             |
| Head of Household   | $6,800             |

## Sources

- Massachusetts DOR, "2026 Massachusetts Income Tax Withholding Instructions",
  Publication MW-1 / Employer's Tax Guide.
- Form M-4, "Employee's Withholding Exemption Certificate".
- Massachusetts General Laws, ch. 62, § 5B (4% surtax on income over $1,000,000,
  effective tax year 2023).
