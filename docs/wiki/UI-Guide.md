# UI Guide

PaycheckCalculator ships two front-ends that share the same Core engine:

- **PaycheckCalculator.App** — .NET MAUI app for Android, iOS, macOS (Mac Catalyst), and Windows.
- **PaycheckCalculator.Blazor** — Blazor Server web app.

Both front-ends expose the paycheck calculator, gross-up mode, annual projection, saved paycheck comparison, exports, budgeting, and optional account sync. The layouts differ, but the calculation and sync models are shared.

---

## MAUI Navigation

The MAUI app uses a Shell `TabBar` with five tabs.

| Tab | Route | Page | Purpose |
|---|---|---|---|
| Inputs | `Inputs` | `InputsPage` | Pay, federal, state, and deduction inputs |
| Results | `Results` | `ResultsPage` | Per-paycheck and annual results, chart, explanations, export/print |
| Paychecks | `Paychecks` | `PaychecksPage` | Saved paycheck list and A/B comparison |
| Budget | `Budget` | `BudgetPage` | Budget methods, categories, transactions, recurring bills, savings goals, reports |
| Account | `Account` | `AccountPage` | Optional sign-in, sync, sign-out, and server URL |

Inputs, Results, and Paychecks share the singleton `CalculatorViewModel`. Budget and Account use their own view models.

---

## Inputs Page

The Inputs page has four sections.

### Pay & Hours

- Calculation mode: standard paycheck, gross-up, bonus / supplemental wage, or self-employment / 1099.
- Pay type: hourly or salary (shown when mode is Standard or Gross-Up).
- Pay frequency: Daily, Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, plus 53-week and 27-biweekly variants.
- Hourly fields: rate, regular hours, overtime hours, overtime multiplier.
- Salary fields: salary amount and salary basis.
- Paycheck number for annual projection.

### Federal W-4

- Filing status.
- Step 2 checkbox.
- Step 3 credits.
- Step 4(a) other income.
- Step 4(b) deductions.
- Step 4(c) extra withholding.

### State

- State picker populated from `StateCalculatorRegistry.SupportedStates`.
- Dynamic fields rendered from the selected state's schema.
- Validation messages returned by the selected state calculator.

State values are collected into `StateInputValues` and passed to Core.

### Deductions

Each deduction has:

- Name.
- Amount.
- Amount type: dollar or percentage.
- Deduction type: pre-tax or post-tax.
- Pre-tax wage-base flags for federal, state, and FICA taxable wages.

---

## Results Page

The Results page shows a per-paycheck view and an annual view.

### Per Paycheck

The per-paycheck result includes:

- Gross pay.
- Federal taxable income.
- FICA taxable wages.
- State taxable wages.
- Federal withholding.
- Social Security.
- Medicare.
- Additional Medicare when applicable.
- State income tax.
- State disability / paid-leave premium when applicable.
- Pre-tax and post-tax deductions.
- Net pay.

Gross-up mode also shows the desired net pay, required gross pay, and gross-up cost.

Bonus / supplemental-wage mode shows the bonus amount, federal flat-rate withholding (22% or 37%), Social Security, Medicare, Additional Medicare, state supplemental withholding (or a caveat when the state has no flat supplemental rate), and net bonus.

Self-employment / 1099 mode shows the net self-employment income, self-employment tax (Social Security + Medicare on 92.35% of earnings), estimated state income tax, take-home (before federal income tax), and a quarterly estimated-payment (Form 1040-ES) schedule with federal and state amounts per due date.

### Show Your Work

Result lines expose explanation details from Core's `PaycheckExplanation` model. The UI opens those details from line-level info actions.

### Chart

The doughnut chart visualizes the gross-pay breakdown across net pay, taxes, state premiums, and deductions.

### Export and Print

Actions are enabled after a calculation:

- Print.
- Export PDF.
- Export CSV.

Exports include per-paycheck results and annual projection. If an A/B comparison is selected, comparison data is appended. The export file-name box controls the output name.

### Annual

The annual view includes:

- Pay periods per year.
- Current paycheck number and remaining paychecks.
- Annualized gross, deductions, taxable wages, withholding, and net pay.
- Projected YTD values.
- Estimated annual liability and over/under withholding.

---

## Paychecks Page

The Paychecks page manages saved paycheck snapshots.

- Saved paychecks store the input and flattened result.
- Selecting two saved paychecks creates an A/B comparison.
- The comparison card has its own CSV/PDF export.
- MAUI persists saved paychecks on device.
- Account sync can merge saved paychecks across clients.

---

## Budget Page

The Budget page is backed by `BudgetViewModel` and the Core budgeting engine.

Features:

- Monthly net income entry or seeding from the latest paycheck.
- Budget method picker: 50/30/20, Zero-Based, Envelope, Custom.
- Template application.
- Category editing.
- Expense transactions.
- Recurring bills.
- Savings goals.
- Summary totals for budgeted, spent, remaining, unallocated, recurring, and savings contribution amounts.
- Zero-based allocation status.
- Budget report generation/export when available.

### Recurring bills

Recurring bills collect name, category, amount, frequency, and optional due-day information. The UI shows both the bill amount and monthly-equivalent amount.

### Savings goals

Savings goals collect name, target amount, current saved amount, and optional target date. The UI shows remaining monthly contribution needed when a target date exists.

### Reports

Budget reports show budget-vs-actual and spend-by-category history. The UI is controlled by `IEntitlementProvider`, so reports are present in the codebase but gated until entitlement support is enabled.

---

## Account Page

The Account page is optional. It supports:

- Account creation.
- Sign-in.
- Manual sync.
- Sign-out.
- Editable sync server URL.

Without an account, the app still works locally. With an account, saved paychecks and budget-related state sync through `PaycheckCalculator.Api`.

---

## Blazor Web App

The Blazor app mirrors the MAUI feature set in a web layout.

Main areas:

- Home page.
- Per-state SEO landing pages.
- Calculator page.
- Saved Paychecks & Account panel.
- Budget page.
- Sitemap and robots endpoints.

The calculator page displays inputs and results side by side. It includes four calculation modes (standard, gross-up, bonus/supplemental wage, self-employment), YTD Social Security and Medicare wage inputs, exports, printing, saved paycheck comparison, and annual projection.

The Budget page includes the same budget methods, categories, recurring bills, savings goals, transactions, summaries, and report UI as MAUI.

Anonymous Blazor saved paychecks and budget state live in circuit memory until the browser session ends. Signing in syncs them to the server.

---

## Input Formatting and Theming

- `DecimalFormatBehavior` formats numeric MAUI inputs.
- `EnumDisplay` provides friendly labels such as `Biweekly` to `Bi-Weekly`.
- `StateFieldViewModel` adapts schema-driven fields to MAUI controls.
- Blazor renders dynamic state fields from the same schema model.
- Result charts and category bars use fixed UI palettes defined in the front-end layers.
