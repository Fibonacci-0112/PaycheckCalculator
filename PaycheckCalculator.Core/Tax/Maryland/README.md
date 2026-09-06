# Maryland (MD)

Maryland state and county income tax withholding is computed by the dedicated
`MarylandWithholdingCalculator` in this folder.

## Formula (2026 Comptroller of Maryland Employer Withholding Guide)

Maryland's percentage method works on **one payroll period at a time** — it never
annualizes. The guide states the formula (page 10) as:

```
Total wages (before any deductions)
LESS  Allowance for Standard Deduction
LESS  Value of exemptions (count × the amount for one exemption
      for the applicable payroll period)
Equals TAXABLE INCOME
```

1. Per-period state taxable wages (gross minus pre-tax deductions).
2. Below the payroll period's **"DO NOT WITHHOLD ON GROSS WAGES LESS THAN"**
   threshold — the $5,000 annual figure prorated — nothing is withheld, state or
   county.
3. Subtract the period's share of the **flat $3,400 standard deduction**.
4. Subtract the MW507 **exemptions** (count × the period's exemption value).
5. Apply the state rate schedule to the result, with the period's bracket
   thresholds.
6. Apply the county rate to that same taxable income.
7. Round each line to the cent, then add any extra per-period withholding
   elected on Form MW507.

### Per-payroll-period allowances (guide page 10)

| Payroll period | One exemption | Standard deduction | No withholding below |
|---|---|---|---|
| Daily        | $8.77     | $9.31     | $13.70    |
| Weekly       | $61.54    | $65.38    | $96.00    |
| Bi-weekly    | $123.08   | $130.76   | $192.00   |
| Semi-monthly | $133.33   | $141.66   | $208.00   |
| Monthly      | $266.67   | $283.33   | $417.00   |
| Quarterly    | $800.00   | $850.00   | $1,250.00 |
| Annually     | $3,200.00 | $3,400.00 | $5,000.00 |

These are taken from the guide as printed rather than recomputed, so results
match the published tables to the cent. The guide publishes no semiannual row,
so that one is derived from the annual amounts.

### Two rules that differ from the annual return

- **4.75% minimum rate.** *"Maryland law does not permit the use of a rate of
  less than 4.75% to be used for withholding tax purposes"* (Withholding Tax
  Facts 2026). The 2%/3%/4% brackets that apply on Form 502 are therefore not
  used: withholding starts at 4.75% from the first dollar.
- **Flat standard deduction.** *"The standard deduction amounts have changed per
  new legislation enacted in the 2025 Legislative Session. For the purpose of the
  percentage method calculation the Standard Deduction is $3,400"* (guide page
  2). It no longer varies with wages or filing status, so Single and Married
  differ only where the wider joint brackets bite.

### State withholding rate schedule (annual ceilings, scaled to the period)

| Rate | Single / MFS / dependent | MFJ / HoH / qualifying surviving spouse |
|---|---|---|
| 4.75% | to $100,000   | to $150,000   |
| 5.00% | to $125,000   | to $175,000   |
| 5.25% | to $150,000   | to $225,000   |
| 5.50% | to $250,000   | to $300,000   |
| 5.75% | to $500,000   | to $600,000   |
| 6.25% | to $1,000,000 | to $1,200,000 |
| 6.50% | above         | above         |

## Filing statuses (MW507)

The guide prints two rate columns; the three MW507 statuses map onto them.

| Status            | Rate schedule used |
|-------------------|--------------------|
| Single            | Single (also MFS and dependent filers) |
| Married           | Joint              |
| Head of Household | Joint (also qualifying surviving spouse) |

## County income tax

Maryland also levies a county income tax, and unlike the optional local taxes
in most states **every Maryland employee pays one**. It applies to the same
taxable income the state schedule uses, and is surfaced as its own `CountyIncome`
tax line rather than folded into the state figure.

Rates live in `Data/md_county_rates_2026.json`, from the county listing in the
Comptroller's *Withholding Tax Facts, January 2026 – December 2026*. For 2026
they run from 2.25% (Worcester, and the special nonresident rate) to 3.30%
(Dorchester, Kent); Allegany and Kent changed for 2026.

Two counties apply **graduated** rates whose brackets differ by filing status.
They are marginal, like the state schedule, and their annual ceilings are scaled
to the payroll period the same way:

| County | Single / MFS / dependent | MFJ / HoH / qualifying surviving spouse |
|---|---|---|
| Anne Arundel | 2.70% to $50,000, 2.94% to $400,000, then 3.20% | 2.70% to $75,000, 2.94% to $480,000, then 3.20% |
| Frederick | 2.25% to $25,000, 2.75% to $50,000, 2.96% to $150,000, then 3.20% | 2.25% to $25,000, 2.75% to $100,000, 2.96% to $250,000, then 3.20% |

The county comes from the `County` picker in `Data/Schemas/md.json`. When the
employee reports none, withholding uses the highest local rate (3.30%) —
the Comptroller's own instruction for that case — so the default is
`Unknown Maryland County`, listed first in the schema so the MAUI picker's
first-option fallback lands on the same safe value.

## Why the printed tables are not used directly

The guide ships ten percentage tables (2.25%, 2.40%, 2.65%, 2.75%, 2.85%, 3.00%,
3.05%, 3.10%, 3.20%, 3.30%) and tells manual filers to *"use the table that
agrees with, or is closest to, without going below the actual local tax rate."*
Each table is simply the state schedule **plus a flat local rate on the same
taxable income**, so this calculator computes the two components directly. That
reproduces the tables and lets each county's *actual* rate be used instead of the
rate group it rounds up into.

Worked example — single, $1,600 bi-weekly, Carroll County (3.03%), no exemptions:

```
taxable income = $1,600.00 − $130.76      = $1,469.24
state          = $1,469.24 × 4.75%        =    $69.79
county         = $1,469.24 × 3.03%        =    $44.52
```

The printed 3.05% table (page 31) reaches the same total through its combined
7.80% first-band rate — 4.75% state plus the 3.05% group Carroll's 3.03% rounds
up into — so a manual lookup lands a few cents higher.

## Known divergence from the published tables

Because the state and county lines are shown and rounded separately, their sum
can differ by a cent from a table's single combined figure. That split is what a
paycheck actually shows, and matches how ADP and PaycheckCity report Maryland.

The daily row reproduces an inconsistency in the guide itself: its daily
allowances are the annual amounts over 365 days, while the bracket thresholds in
its daily tables are the annual amounts over 364 (52 weeks × 7 days). Both are
implemented as published.

Maryland residents who work in Delaware use a separate combined table that is
not implemented; that is disclosed as an exclusion on every Maryland result.
