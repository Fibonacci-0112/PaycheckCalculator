# South Carolina (SC)

South Carolina state income tax withholding is computed by the dedicated
`SouthCarolinaWithholdingCalculator` class in this folder.

## Formula (SCDOR Form WH-1603F, 2026)

1. **Annualize wages** — multiply per-period gross wages by the number of pay
   periods per year.
2. **Standard deduction** — if the employee claims at least one allowance,
   subtract 10% of annualized wages, not to exceed **$7,500**. When zero
   allowances are claimed, no standard deduction applies.
3. **Personal allowance deduction** — subtract **$5,000** for each allowance
   claimed on Form SC W-4 Line 2.
4. **Floor** — taxable income cannot be negative.
5. **Apply graduated brackets** (same schedule for all filing statuses):

   | Annualized taxable income | Rate |
   |---------------------------|------|
   | $0 – $3,640               |  0%  |
   | $3,640 – $18,230          |  3%  |
   | Over $18,230              |  6%  |

6. **De-annualize** — divide annual tax by pay periods and round to two
   decimal places (half-up).
7. **Additional withholding** — add any extra per-period amount the employee
   requested on Form SC W-4.

## Form SC W-4 filing statuses

| SC W-4 option                        | Calculator status  |
|--------------------------------------|--------------------|
| Single / Married Filing Separately   | Single             |
| Married Filing Jointly               | Married            |
| Head of Household                    | Head of Household  |

All three statuses use the identical bracket schedule.

## Sources

- South Carolina Department of Revenue, Form WH-1603F, _Employer's Withholding
  Tax Formula_, 2026.
- South Carolina Form SC W-4, _Employee's Withholding Certificate_.
- SC Code Ann. § 12-6-510; SC Act R. 117-40.
