# Nebraska (NE)

Nebraska state income tax withholding is computed by the dedicated
`NebraskaWithholdingCalculator`, which implements the annualized percentage-method
formula described in the Nebraska Department of Revenue Circular EN and Form W-4N
(Nebraska Employee's Withholding Allowance Certificate).

## Filing statuses (Form W-4N)

| Status | Standard Deduction |
|---|---|
| Single / Married Filing Separately | $8,600 |
| Married Filing Jointly / Qualifying Surviving Spouse | $17,200 |
| Head of Household | $12,900 |

## Allowance credit

Nebraska uses a **tax credit** (not an income deduction) of **$171 per allowance**
claimed on Form W-4N.  The credit is subtracted from computed annual tax after
applying the brackets, and the result is floored at zero.

## 2026 tax brackets

### Single / Married Filing Separately

| Income | Rate |
|---|---|
| $0 – $4,030 | 2.46% |
| $4,030 – $24,120 | 3.51% |
| $24,120 – $38,870 | 5.01% |
| Over $38,870 | 5.2% |

### Married Filing Jointly / Qualifying Surviving Spouse

| Income | Rate |
|---|---|
| $0 – $8,040 | 2.46% |
| $8,040 – $48,250 | 3.51% |
| $48,250 – $77,730 | 5.01% |
| Over $77,730 | 5.2% |

### Head of Household

| Income | Rate |
|---|---|
| $0 – $6,060 | 2.46% |
| $6,060 – $36,180 | 3.51% |
| $36,180 – $58,310 | 5.01% |
| Over $58,310 | 5.2% |

## Sources

- Nebraska Department of Revenue, Circular EN, 2026.
- Form W-4N, Nebraska Employee's Withholding Allowance Certificate.
- Nebraska LB 754 (2023): phased income tax rate reductions.

