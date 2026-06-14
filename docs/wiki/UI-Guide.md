# UI Guide

PaycheckCalc is a two-tab .NET MAUI app: enter pay details on **Inputs**, view the breakdown on **Results**. This page describes the pages, input forms, and features.

---

## Navigation

The app uses a **Shell TabBar** with two tabs:

| Tab | Route | Page |
|---|---|---|
| Inputs | `Inputs` | `InputsPage` — four-section input form |
| Results | `Results` | `ResultsPage` — per-period and annual results |

Both pages bind to the same singleton `CalculatorViewModel`, so inputs and results stay in sync as you switch tabs.

---

## Inputs Page

The Inputs page is a single page with four sections selected by a top button strip. Every section ends with a **Calculate** button.

### Pay & Hours

- **Pay Frequency** — Picker: Weekly, Biweekly, Semimonthly, Monthly, Quarterly, Semiannual, Annual, Daily.
- **Hourly Rate** — Decimal input.
- **Regular Hours** — Decimal input (per pay period).
- **Overtime Hours** — Decimal input (per pay period).
- **Overtime Multiplier** — Decimal input (defaults to 1.5).
- **Paycheck Number** — 1-based integer for annual projection.

### Federal

- **Filing Status** — Picker: Single / Married Filing Separately, Married Filing Jointly, or Head of Household (see `FederalFilingStatus`).
- **Step 2 Checkbox** — Toggle (two jobs / spouse works).
- **Step 3 Credits** — Decimal (child/dependent tax credits).
- **Step 4(a) Other Income** — Decimal.
- **Step 4(b) Deductions** — Decimal.
- **Step 4(c) Extra Withholding** — Decimal.

### State

- **State** — Picker populated from `StateCalculatorRegistry.SupportedStates` (all 50 + DC).
- **Dynamic Fields** — Rendered from the selected state's `GetInputSchema()`. Fields update automatically when the state selection changes, and previously entered values are cached per state.

Common dynamic fields include:
- Filing Status (picker with state-specific options)
- Allowances / Exemptions (integer)
- Dependents (integer, for states like Alabama)
- Extra Withholding (decimal)

State-level validation errors (from the calculator's `Validate` method) are shown beneath the fields, and calculation is blocked until they are fixed.

### Deductions

- **Add Deduction** — Creates a new deduction entry.
- Each deduction has:
  - **Name** — Free-text label.
  - **Amount** — Dollar or percentage value.
  - **Amount Type** — Picker: Dollar or Percentage.
  - **Type** — Picker: Pre-Tax or Post-Tax.
  - **Reduces Federal / State / FICA Taxable Income** — Toggles (for pre-tax deductions).

---

## Results Page

The Results page has two sub-tabs:

### Period Tab

Displays the per-paycheck breakdown:

- Gross Pay
- Federal Taxable Income, FICA Taxable Income, State Taxable Income
- Federal Tax, Social Security Tax, Medicare Tax, State Income Tax
- State Disability Insurance (when applicable, with dynamic label)
- Pre-Tax / Post-Tax Deductions (when present)
- **Net Pay**

Additional features:
- **"Show Your Work" Explanations** — Tap the ⓘ icon next to a line (or the Net Pay card) for a step-by-step breakdown of how that amount was computed, including formulas and source references.
- **Doughnut Chart** — Visual breakdown of gross pay by category (federal tax, state tax, SS, Medicare, net pay).

#### Export & Print

The Results page toolbar exposes three actions, each enabled once a paycheck has been calculated. An **export file-name box** (prefilled from the paycheck name on each calculation, and editable) names the exported file; blank falls back to `Paycheck-Summary`. Every export covers the **per-period breakdown plus the annual projection** (annualized amounts, projected YTD, and the year-end estimate), and **appends the A/B comparison table when two saved paychecks are selected** on the Paychecks page.

- **Print** — Renders the summary and opens the native print dialog (Android print framework / Windows default PDF handler). Backed by `Services/Printing`.
- **Export PDF** — Writes a PDF (one or more Letter-size pages) and opens it in a viewer (preferring Adobe Reader/Acrobat). Backed by `Services/Pdf`.
- **Export CSV** — Writes a CSV of the breakdown (Income, Taxes, Deductions, Summary, Annualized, Projected YTD, Year-End Estimate, and any comparison) and opens it in the platform's default CSV application (Microsoft Excel on Windows, or the chosen default spreadsheet app on Android). Amounts are plain decimals for clean spreadsheet import. Backed by `Services/Csv`.

The Paychecks page's A/B comparison card also has its own **Export PDF / Export CSV** buttons that export the comparison table on its own (`RenderComparison`).

> **Note on layering:** the renderers (`PaycheckPdfRenderer` / `PaycheckCsvRenderer`) take the already-built `ResultCardModel`, the optional `AnnualProjectionModel`, and optional `ComparisonRow`s and return bytes/text; the export services (`Services/Pdf`, `Services/Csv`, `Services/Printing`) just persist those bytes under the chosen file name and hand them to the platform launcher.

> **Web app:** The Blazor results panel offers the same three actions plus the same editable file-name box. **Export CSV** and **Export PDF** download the breakdown (per-period + annual projection, with the comparison appended when an A/B pair is selected) via the browser (rendered by `PaycheckCalc.Blazor/Services/Export/`, matching the MAUI format), and **Print** opens the browser print dialog using a `@media print` stylesheet that isolates the results — so it prints whichever tab (Per Paycheck or Annual) is on screen. The Compare A/B panel has its own CSV/PDF buttons for a comparison-only export.

### Annual Tab

Displays annualized projections computed by `AnnualProjectionCalculator`:

- Pay periods per year, current paycheck number, and remaining paychecks.
- Annualized gross, deductions, taxable wages, withholdings, FICA, and net pay.
- Projected year-to-date totals through the current paycheck number.
- Estimated annual federal/FICA liability and the projected year-end over/under withholding.

---

## Input Formatting

The app uses `DecimalFormatBehavior` to format numeric inputs consistently. Enum values use `EnumDisplay` helpers for user-friendly picker labels (e.g., `Biweekly` → "Bi-Weekly").

---

## Theming

The app uses a blue-themed color scheme:
- Primary: `#1565C0` (toolbar, tab bar)
- Page background: `#F0F4FF`
- Tab bar: White text on blue background
