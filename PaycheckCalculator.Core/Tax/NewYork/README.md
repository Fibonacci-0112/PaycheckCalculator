# New York (NY)

New York state income tax withholding is computed by the dedicated
`NewYorkWithholdingCalculator`, which implements the annualized
percentage-method formula from New York Publication NYS-50-T-NYS (2026)
and Form IT-2104 (Employee's Withholding Allowance Certificate).

## Filing statuses (IT-2104)

| Status | Standard deduction | Brackets used |
|---|---|---|
| Single (or Married Filing Separately) | $8,000 | Single/HoH brackets |
| Married (Filing Jointly or QW) | $16,050 | Married brackets |
| Head of Household | $11,000 | Single/HoH brackets |

## Allowances

Each IT-2104 allowance reduces annual taxable wages by **$1,000**.

## 2026 tax brackets

### Single / Head of Household

| Taxable income (annual) | Rate |
|---|---|
| $0 – $8,500 | 4.00% |
| $8,500 – $11,700 | 4.50% |
| $11,700 – $13,900 | 5.25% |
| $13,900 – $21,400 | 5.90% |
| $21,400 – $80,650 | 6.09% |
| $80,650 – $215,400 | 6.41% |
| $215,400 – $1,077,550 | 6.85% |
| $1,077,550 – $5,000,000 | 9.65% |
| $5,000,000 – $25,000,000 | 10.30% |
| Over $25,000,000 | 10.90% |

### Married Filing Jointly / Qualifying Widow(er)

| Taxable income (annual) | Rate |
|---|---|
| $0 – $17,150 | 4.00% |
| $17,150 – $23,600 | 4.50% |
| $23,600 – $27,900 | 5.25% |
| $27,900 – $43,000 | 5.90% |
| $43,000 – $161,550 | 6.09% |
| $161,550 – $323,200 | 6.41% |
| $323,200 – $2,155,350 | 6.85% |
| $2,155,350 – $5,000,000 | 9.65% |
| $5,000,000 – $25,000,000 | 10.30% |
| Over $25,000,000 | 10.90% |

## Sources

- New York State Department of Taxation and Finance, Publication
  NYS-50-T-NYS (New York State Withholding Tax Tables and Methods), 2026.
- Form IT-2104, Employee's Withholding Allowance Certificate.
