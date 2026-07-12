# Wisconsin (WI)

Wisconsin state income tax withholding is computed by the dedicated
`WisconsinWithholdingCalculator` in this folder.

The calculator implements the annualized percentage-method formula described in
the Wisconsin Department of Revenue *Employer's Withholding Tax Guide*
(Publication W-166, 2026) and the accompanying Circular WT.

## Form

**WT-4** — Employee's Wisconsin Withholding Exemption Certificate

## Filing statuses

| WT-4 box | Status            |
|----------|-------------------|
| 1        | Single            |
| 2        | Married           |
| 3        | Head of Household |

## 2026 parameters

| Parameter                    | Single   | Married  | Head of Household |
|------------------------------|----------|----------|-------------------|
| Standard deduction           | $12,760  | $23,170  | $16,840           |
| Per-allowance deduction      | $700     | $700     | $700              |

## Tax brackets

Single and Head of Household (same thresholds):

| Rate   | From      | To         |
|--------|-----------|------------|
| 3.54%  | $0        | $13,810    |
| 4.65%  | $13,810   | $27,630    |
| 5.30%  | $27,630   | $304,170   |
| 7.65%  | $304,170  | —          |

Married:

| Rate   | From      | To         |
|--------|-----------|------------|
| 3.54%  | $0        | $18,410    |
| 4.65%  | $18,410   | $36,820    |
| 5.30%  | $36,820   | $405,550   |
| 7.65%  | $405,550  | —          |

## Sources

- Wisconsin Department of Revenue, *Employer's Withholding Tax Guide*
  (Publication W-166), effective 2026.
- Wisconsin Department of Revenue, Circular WT (2026 withholding tables
  and percentage-method formula).
