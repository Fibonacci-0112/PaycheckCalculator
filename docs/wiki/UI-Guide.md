# UI Guide

PaycheckCalc ships two front-ends that share the same `PaycheckCalc.Core` engine:

- A **.NET MAUI** app (Android & Windows) — a five-tab Shell.
- A **Blazor Server** web app — a single calculator page plus per-state SEO landing pages and a budget page.

This page focuses on the MAUI app's pages, input forms, and features, with notes on the web app where it
differs.

---

## MAUI Navigation

The app uses a **Shell `TabBar`** with five tabs, all bound to dependency-injected view models:

| Tab | Route | Page | Purpose |
|---|---|---|---|
| Inputs | `Inputs` | `InputsPage` | Four-section input form (Pay & Hours, Federal, State, Deductions) |
| Results | `Results` | `ResultsPage` | Per-period and annual results, doughnut chart, export/print |
| Paychecks | `Paychecks` | `PaychecksPage` | Saved-paycheck list and A/B comparison |
| Budget | `Budget` | `BudgetPage` | Monthly budget tracker (50/30/20, categories, transactions) |
| Account | `Account` | `AccountPage` | Optional sign-in / account creation / sync / server URL |

The Inputs and Results pages share the singleton `CalculatorViewModel`, so inputs and results stay in
sync as you switch tabs. The Budget and Account pages have their own `BudgetViewModel` and
`AccountViewModel`.

---

## Inputs Page

A single page with four sections selected by a top button strip. Every section ends with a **Calculate**
button.

### Pay & Hours

- **Calculation Mode** — Switches between a standard paycheck and a **gross-up** (enter a desired net /
  take-home amount and the app solves for the gross pay that delivers it after all withholding).
- **Pay Type** — Hourly or Salary. Hourly uses rate × hours; Salary takes an annual or per-period amount.
- **Pay Frequency** — Picker: Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual,
  Daily (plus the 53-week and 27-biweekly variants used by some payroll calendars).
- **Hourly Rate**, **Regular Hours**, **Overtime Hours**, **Overtime Multiplier** (defaults to 1.5) — for
  hourly pay.
- **Salary Amount** + **Salary Basis** (per year / per period) — for salaried pay.
- **Paycheck Number** — 1-based integer for the annual projection.

### Federal (W-4)

- **Filing Status** — Picker: Single / Married Filing Separately, Married Filing Jointly, or Head of
  Household (`FederalFilingStatus`; Single and MFS share one Pub 15-T value).
- **Step 2 Checkbox** — Toggle (two jobs / spouse works).
- **Step 3 Credits** — Annual child/dependent tax credits.
- **Step 4(a) Other Income**, **Step 4(b) Deductions**, **Step 4(c) Extra Withholding**.

### State

- **State** — Picker populated from `StateCalculatorRegistry.SupportedStates` (all 50 + DC).
- **Dynamic Fields** — Rendered from the selected state's `GetInputSchema()` (filing status, allowances,
  exemptions, dependents, extra withholding, etc.). Fields update automatically when the state changes,
  and previously entered values are cached per state.

State-level validation errors (from the calculator's `Validate` method) are shown beneath the fields, and
calculation is blocked until they are fixed.

### Deductions

- **Add Deduction** — Creates a new deduction entry. Each deduction has a **Name**, **Amount**, **Amount
  Type** (Dollar or Percentage of gross), **Type** (Pre-Tax or Post-Tax), and, for pre-tax deductions,
  toggles for whether it **Reduces Federal / State / FICA Taxable Income** (e.g. 401(k) reduces federal &
  state but not FICA; Section 125 cafeteria plans reduce all three).

---

## Results Page

A **Per Paycheck / Annual** sub-tab. Before the first calculation, a friendly empty state is shown.

### Per Paycheck

- Gross Pay; Federal / FICA / State Taxable Income.
- Federal Tax, Social Security, Medicare (and Additional Medicare when applicable), State Income Tax.
- State Disability / Paid-Leave Insurance (when applicable, with a dynamic label — CA SDI, CO FMLI,
  CT FLI, WA Cares).
- Pre-Tax / Post-Tax Deductions (when present).
- **Net Pay**.
- In gross-up mode the page leads with a summary (desired net, taxes & deductions covered, required gross).

Additional features:

- **"Show Your Work" Explanations** — Tap the ⓘ icon next to a line (or the Net Pay card) for a
  step-by-step breakdown with formulas and source references, served from the `Explanation/` records on
  `PaycheckResult`.
- **Doughnut Chart** — Visual breakdown of gross pay by category (federal tax, Social Security, Medicare,
  state tax, state disability, pre-/post-tax deductions, net pay) rendered by `DoughnutChartDrawable`.

#### Export & Print

The toolbar exposes three actions, enabled once a paycheck is calculated. An **export file-name box**
(prefilled from the paycheck name, editable; blank falls back to `Paycheck-Summary`) names the file. Every
export covers the **per-period breakdown plus the annual projection**, and **appends the A/B comparison
table when two saved paychecks are selected** on the Paychecks page.

- **Print** — Renders the summary and opens the native print dialog (`Services/Printing`).
- **Export PDF** — Writes a Letter-size PDF via a built-in minimal PDF writer (no external packages) and
  opens it in a viewer (`Services/Pdf`).
- **Export CSV** — Writes a CSV (Income, Taxes, Deductions, Summary, Annualized, Projected YTD, Year-End
  Estimate, and any comparison) and opens it in the default spreadsheet app (`Services/Csv`).

> **Layering:** the renderers (`PaycheckPdfRenderer` / `PaycheckCsvRenderer`) take the already-built
> `ResultCardModel`, an optional `AnnualProjectionModel`, and optional `ComparisonRow`s and return
> bytes/text; the export services just persist those bytes under the chosen file name and hand them to the
> platform launcher.

### Annual

Annualized projections computed by `AnnualProjectionCalculator`:

- Pay periods per year, current paycheck number, and remaining paychecks.
- Annualized gross, deductions, taxable wages, withholdings, FICA, and net pay.
- Projected year-to-date totals through the current paycheck number.
- Estimated annual federal/FICA liability and the projected year-end over/under withholding.

---

## Paychecks Page

The saved-paychecks list and A/B comparison.

- Saved paychecks (calculated results stored with their input) are listed with their key figures.
- Selecting two entries shows an **A/B side-by-side comparison** (`ComparisonRow`s) highlighting the
  Net Pay difference.
- The comparison card has its own **Export PDF / Export CSV** buttons for a comparison-only export
  (`RenderComparison`).

On MAUI, saved paychecks are persisted **on device** (`saved-paychecks.json`) so they survive restarts
with or without an account. See [Accounts & Sync](Accounts-and-Sync.md).

---

## Budget Page

A simple monthly budget tracker built on the Core `Budgeting` engine. See [Budgeting](Budgeting.md) for
the full design.

- **Monthly net income** — seed it from the current paycheck (the app normalizes per-period net pay to a
  monthly figure via `MonthlyIncomeNormalizer`) or enter it directly.
- **Apply 50/30/20** — populates Needs / Wants / Savings categories at 50% / 30% / 20% of monthly income.
- **Categories** — each has a name, a type (Needs / Wants / Savings), and an amount expressed as a fixed
  dollar value or a percentage of monthly income.
- **Transactions** — record expenses against a category; the page shows budgeted vs. spent vs. a
  **projected month-end** (a linear run-rate extrapolation based on the day of the month).

Budgets and transactions are persisted on device (`budgets.json`) and sync with an account.

---

## Account Page

Optional accounts let saved paychecks and budgets sync across the MAUI and Blazor front-ends.

- **Sign in / create account** — email + password against the sync API.
- **Server URL** — editable (stored in `Preferences`, default `http://localhost:5201`; from the Android
  emulator the host is reachable at `http://10.0.2.2:5201`).
- **Sync now / sign out** — manual sync and sign-out; background syncs are coalesced by `SyncCoordinator`.

Everything works fully **without** an account — the Account tab is purely opt-in.

---

## Web app (Blazor)

The Blazor Server app mirrors the MAUI calculator in a single page with the inputs and results panels side
by side, plus:

- A **Home** page (`/`) and per-state **SEO landing pages** (`/{state}-paycheck-calculator`, e.g.
  `/california-paycheck-calculator`) generated from `StateMetadata`, with a `/sitemap.xml` and
  `robots.txt`.
- A **Budget** page (`/budget`) mirroring the MAUI budget tracker.
- A **Saved Paychecks & Account** panel with the same A/B comparison and CSV/PDF/Print exports (browser
  download + `window.print()` driven by `wwwroot/export.js` and a `@media print` stylesheet in `app.css`).

Anonymous Blazor saved paychecks and budgets live **only until the browser tab closes** (circuit memory);
signing in syncs them to the server.

---

## Input Formatting & Theming

- `DecimalFormatBehavior` formats numeric inputs (currency / percentage modes); `EnumDisplay` provides
  friendly picker labels (e.g. `Biweekly` → "Bi-Weekly").
- The MAUI Shell uses a **dark tab bar** (`#333333` background, white selected text, `#9E9E9E` unselected).
  Result/chart line items use a fixed palette (federal red `#C62828`, Social Security blue `#1565C0`,
  Medicare purple `#6A1B9A`, state orange `#EF6C00`, disability amber `#F9A825`, net green `#2E7D32`).
