# UI Guide

PaycheckCalculator ships two front-ends that share the same Core engine:

- **PaycheckCalculator.App** — .NET MAUI app for Android, iOS, macOS (Mac Catalyst), and Windows.
- **PaycheckCalculator.Blazor** — Blazor Server web app.

Both front-ends expose the paycheck calculator, gross-up mode, annual projection, saved paycheck comparison, exports, budgeting, and optional account sync. The layouts differ, but the calculation and sync models are shared.

---

## Design system

Both front-ends are built on one shared token set, so they read as the same product.

| Surface | Token source |
|---|---|
| MAUI | `Resources/Styles/Colors.xaml` (palette) and `Resources/Styles/Styles.xaml` (keyed + implicit styles), merged from `App.xaml` |
| Blazor | the `:root` custom-property block at the top of `wwwroot/app.css` |

The token names match across the two files (`Primary`/`--primary`, `Ink`/`--ink`, `BorderSubtle`/`--border`, and so on). Pages reference tokens rather than hex literals; the only intentional exceptions are the chart slice colours, which are written into an SVG `fill` attribute (Blazor) or set on an `ICanvas` (MAUI) and so cannot read the resource dictionary at draw time.

Key visual conventions:

- Cards are white, `1px` bordered, `16px` radius, with a very soft shadow — bordered rather than floated.
- Currency figures are the largest type on any surface; uppercase micro-labels sit above them.
- Blue is take-home/primary, red is tax, slate is FICA and deductions.

The MAUI keyed styles cover only the patterns that actually repeat: `CardBorder`, `RowSeparator`, `EyebrowLabel`, `FieldLabel`.

### App icon and splash

The MAUI launcher icon is a single `<MauiIcon>` in `PaycheckCalculator.App.csproj`, built from two layers under `Resources/AppIcon/`: `appicon.svg` (the plate gradient, `#0063E9` → `#014DBD`) and `appiconfg.png` (the artwork on transparency). Both are derived from the master at `docs/brand/appicon-source.png` — regenerate rather than hand-edit them.

The foreground is deliberately inset via `ForegroundScale="0.60"`: Android lets launchers mask adaptive icons to any shape, and only the central 66dp circle is guaranteed visible, so the artwork is canvased on its minimum enclosing circle to keep the green check badge out of the crop. `Resources/Splash/splash.svg` carries the wordmark on two lines over the same blue. See `docs/brand/README.md` for the full derivation.

Note that the icon's blue is the artwork's own plate colour, which is deeper than the UI's `Primary` `#2563EB` — the launcher icon is not currently token-driven.

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

The tab bar is white with the primary colour marking selection, and each tab has an icon from `Resources/Images/tab_*.svg`. The brand mark and "Tax Year 2026" subtitle are set once on the Shell via `Shell.TitleView`, which keeps the native nav bar — and therefore the Results page's toolbar actions — intact.

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

The Results page shows a per-paycheck view and an annual view. A blue summary strip above the sub-tabs shows net pay per period and annual take-home; it appears only once a calculation has produced a result.

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

The doughnut chart visualizes the gross-pay breakdown across net pay, taxes, state premiums, and deductions. The centre of the ring shows the share of gross that survives as take-home; each legend row carries a colour dot, label and percentage.

Both front-ends keep all five slices (net pay, federal, state, FICA, deductions) rather than rolling them into coarser groups, so nothing the engine computes is hidden.

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

Without an account, the app still works locally. With an account, saved paychecks and budget-related state sync through `PaycheckCalculator.API`.

---

## Blazor Web App

The Blazor app mirrors the MAUI feature set in a web layout.

### Shell

`MainLayout.razor` renders a fixed left sidebar and a top bar:

- **Sidebar** — brand mark, "Tax Year 2026", and navigation: Calculator (`/`), Budget (`/budget`), Saved Paychecks and Account & Sync (anchors into the calculator page's panels). The Saved Paychecks entry carries a live count badge. Privacy and Terms links sit in the sidebar footer.
- **Top bar** — page title, an IRS-tables status pill sourced from `TaxYearSupport`, and an Export Pay Stub action.

Per-page title and top-bar content are pushed to the layout through `Components/Layout/ShellState.cs`, a cascaded object the page sets in `OnInitialized`. This keeps page concerns out of the layout.

The render mode is set globally on `<Routes>` and `<HeadOutlet>` in `App.razor` rather than per page, because the layout itself needs interactivity for the badge and the export action. Prerendering is unaffected, so the SEO landing pages still serve server-rendered HTML.

### Areas

- Home page.
- Per-state SEO landing pages (these pass a state-specific top-bar title).
- Calculator page.
- Saved Paychecks & Account panel.
- Budget page.
- Sitemap and robots endpoints.

The calculator page leads with a blue hero (annual net take-home, per-period inset, projected-net sparkline) and a four-card KPI row (gross, FICA, federal, state, each with a percent-of-gross badge), both drawn from `AnnualProjection`. Below that, inputs and results sit side by side, with an income-allocation chart card, a setup checklist, and a payroll log.

The setup checklist and the log are driven by real state, not placeholders: the checklist's completed/in-progress/pending steps come from whether pay is entered, the state's schema fields validate, deductions exist, and a calculation has been saved; the log's rows, search and All/Hourly/Salary/Gross-Up filters all read the session's `SavedPaycheckDto` list.

The calculator page displays inputs and results side by side. It includes five calculation modes (standard, gross-up, bonus/supplemental wage, self-employment, and the hourly ↔ salary converter), YTD Social Security and Medicare wage inputs, exports, printing, saved paycheck comparison, and annual projection. The converter is a pure rate conversion, so it has no annual projection, explanations, or exports.

The Budget page includes the same budget methods, categories, recurring bills, savings goals, transactions, summaries, and report UI as MAUI.

Anonymous Blazor saved paychecks and budget state live in circuit memory until the browser session ends. Signing in syncs them to the server.

---

## Input Formatting and Theming

- `DecimalFormatBehavior` formats numeric MAUI inputs.
- `EnumDisplay` provides friendly labels such as `Biweekly` to `Bi-Weekly`.
- `StateFieldViewModel` adapts schema-driven fields to MAUI controls.
- Blazor renders dynamic state fields from the same schema model.
- Result charts and category bars draw from the shared token palette (see **Design system** above).

Neither front-end has a dark theme yet. The token layer is the prerequisite for one: before it existed there was nothing to re-point, and the Blazor doughnut chart hardcoded `fill="white"`. That chart now takes its hole and slice strokes from CSS, so adding a dark theme is a matter of redefining tokens rather than editing markup.
