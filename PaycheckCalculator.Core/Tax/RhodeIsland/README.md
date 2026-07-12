# Rhode Island (RI)

Rhode Island state income tax withholding is implemented by the dedicated
`RhodeIslandWithholdingCalculator` class in this folder.

## Form RI W-4 inputs

| Field | Label | Type | Default |
|---|---|---|---|
| `FilingStatus` | RI W-4 Filing Status | Picker | Single |
| `Exemptions` | RI W-4 Exemptions | Integer | 0 |
| `AdditionalWithholding` | Additional Withholding | Decimal | 0.00 |

## Calculation formula (2026, RI Pub. T-174)

```
taxable wages   = max(0, gross wages − pre-tax deductions)
annual wages    = taxable wages × pay periods per year
annual taxable  = max(0, annual wages − $10,550 − (exemptions × $4,700))
annual tax      = ApplyBrackets(annual taxable)
per-period tax  = round(annual tax ÷ pay periods, 2) + additional withholding
```

### Tax brackets (all filing statuses)

| Rate | Income range |
|------|-------------|
| 3.75% | $0 – $77,450 |
| 4.75% | $77,450 – $176,050 |
| 5.99% | over $176,050 |

Rhode Island uses the same standard deduction ($10,550) and the same
graduated brackets for all filing statuses (Single, Married, and Head of
Household).  The filing status is collected on Form RI W-4 but does not
alter the withholding computation.

## Sources

- Rhode Island Division of Taxation, 2026 Employer's Tax Calendar and
  Withholding Guide (Pub. T-174).
- Form RI W-4, Employee's Withholding Certificate.
