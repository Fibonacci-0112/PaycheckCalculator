# Maryland (MD)

Maryland state income tax withholding is computed by the dedicated
`MarylandWithholdingCalculator` in this folder.

## Formula (2026 Comptroller of Maryland Employer Withholding Guide)

1. Annualize per-period taxable wages (gross minus pre-tax deductions, × pay periods).
2. Compute the **variable standard deduction**: 15% of annual wages, bounded by
   filing-status minimums and maximums:
   - Single / MFS:            minimum **$1,600**, maximum **$2,550**
   - Married / Head of Household: minimum **$3,200**, maximum **$5,100**
3. Subtract the standard deduction and MW507 **exemptions** ($3,200 each).
4. Apply the appropriate **ten-bracket graduated rate schedule** (2%–6.5%).
5. De-annualize, round to two decimal places, and add any extra per-period
   withholding elected by the employee.

## Filing statuses (MW507)

| Status            | Standard deduction limits | Rate schedule used |
|-------------------|--------------------------|-------------------|
| Single            | $1,600 – $2,550          | Single            |
| Married           | $3,200 – $5,100          | Married/HoH       |
| Head of Household | $3,200 – $5,100          | Married/HoH       |

## County income tax

Maryland also levies a county income tax, and unlike the optional local taxes
in most states **every Maryland employee pays one**. It is applied to the same
taxable wages the state brackets use, and surfaced as its own `CountyIncome`
tax line rather than folded into the state figure.

Rates live in `Data/md_county_rates_2026.json`, from Attachment 1 of the
Comptroller's *2026 Maryland State and Local Income Tax Withholding
Information* (February 4, 2026). For 2026 they run from 2.25% (Worcester, and
the special nonresident rate) to 3.30% (Dorchester, Kent).

Two counties apply **graduated** rates whose brackets differ by filing status,
and are treated as marginal, like the state schedule:

| County | Single / MFS / dependent | MFJ / HoH / qualifying surviving spouse |
|---|---|---|
| Anne Arundel | 2.70% to $50,000, 2.94% to $400,000, then 3.20% | 2.70% to $75,000, 2.94% to $480,000, then 3.20% |
| Frederick | 2.25% to $25,000, 2.75% to $50,000, 2.96% to $150,000, then 3.20% | 2.25% to $25,000, 2.75% to $100,000, 2.96% to $250,000, then 3.20% |

The county comes from the `County` picker in `Data/Schemas/md.json`. When the
employee reports none, withholding uses the highest local rate (3.30%) —
the Comptroller's own instruction for that case — so the default is
`Unknown Maryland County`, listed first in the schema so the MAUI picker's
first-option fallback lands on the same safe value.

## Known divergence from the published tables

The 2026 guide's percentage method uses a **flat $3,400 standard deduction**
and folds the lowest brackets into a single combined rate per local-rate group.
This module still applies the variable 15% standard deduction and the full
graduated schedule, so results can differ modestly from the published tables.
That divergence is disclosed as an accuracy note on every Maryland result via
`state-md-regular-2026`. Correcting it means re-deriving the state formula
against the guide and is deliberately left as separate work, following the
correction process in `docs/wiki/Accuracy-and-Source-Governance.md`.

Maryland residents who work in Delaware use a separate combined table that is
not implemented; that is disclosed as an exclusion.

