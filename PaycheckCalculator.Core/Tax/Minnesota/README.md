# Minnesota (MN)

Minnesota state income tax withholding is computed by the dedicated
`MinnesotaWithholdingCalculator` located in this folder.

## Overview

The calculator implements the annualized percentage-method formula described in
the Minnesota Department of Revenue *Withholding Tax Instructions and Tables*
(2026, Pub. 89). It supports the three W-4MN filing statuses (Single, Married,
and Head of Household) and uses separate standard deductions and bracket
thresholds for each status.

## 2026 Parameters

| Parameter | Single | Married | Head of Household |
|---|---|---|---|
| Standard deduction | $15,300 | $30,600 | $23,000 |
| Per-allowance deduction | $5,300 | $5,300 | $5,300 |

### Single brackets

| Rate | Income range |
|------|-------------|
| 5.35% | $0 – $33,310 |
| 6.80% | $33,310 – $109,430 |
| 7.85% | $109,430 – $203,150 |
| 9.85% | Over $203,150 |

### Married brackets

| Rate | Income range |
|------|-------------|
| 5.35% | $0 – $48,700 |
| 6.80% | $48,700 – $193,480 |
| 7.85% | $193,480 – $337,930 |
| 9.85% | Over $337,930 |

### Head of Household brackets

| Rate | Income range |
|------|-------------|
| 5.35% | $0 – $41,010 |
| 6.80% | $41,010 – $164,800 |
| 7.85% | $164,800 – $270,060 |
| 9.85% | Over $270,060 |

## Sources

- Minnesota Department of Revenue, *Withholding Tax Instructions and Tables*, 2026 (Pub. 89)
- Minnesota Department of Revenue press release, 2025-12-16 (income tax brackets, standard
  deduction, and dependent exemption amounts for 2026)
