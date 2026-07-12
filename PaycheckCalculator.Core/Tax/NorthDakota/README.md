# North Dakota (ND)

North Dakota state income tax withholding is computed by a dedicated
`NorthDakotaWithholdingCalculator` located in this folder. The calculator
implements the annualized percentage-method formula described in the North
Dakota Office of State Tax Commissioner 2026 Employer's Withholding Guide.

North Dakota employees use the federal Form W-4 for state withholding
purposes (ND adopted the federal W-4 effective 2020; no separate ND
withholding certificate exists for new hires).

## Key parameters (2026)

| | Single / MFS | Married / QSS | Head of Household |
|---|---|---|---|
| Standard deduction | $15,750 | $31,500 | $23,625 |
| Bracket 1 | 1.10% on $0 – $46,500 | 1.10% on $0 – $78,650 | 1.10% on $0 – $62,100 |
| Bracket 2 | 2.04% on $46,500 – $113,750 | 2.04% on $78,650 – $197,550 | 2.04% on $62,100 – $152,100 |
| Bracket 3 | 2.64% over $113,750 | 2.64% over $197,550 | 2.64% over $152,100 |

## Input schema

| Field | Type | Description |
|---|---|---|
| `FilingStatus` | Picker | Single / Married / Head of Household (required) |
| `AdditionalWithholding` | Decimal | Extra per-period withholding requested on W-4 Step 4(c) |

## Sources

- North Dakota Office of State Tax Commissioner, 2026 Employer's Withholding Guide / Income Tax Withholding Tables.
- Federal Form W-4 (used for ND withholding purposes).

