# PaycheckCalculator

PaycheckCalculator is a US paycheck calculator for 2026 withholding rules. It computes gross pay, net pay, federal withholding, FICA, state withholding, employee-paid state disability / paid-leave premiums, deductions, annual projections, gross-up pay, and self-employment (1099) estimates for all 50 US states plus the District of Columbia.

The solution currently ships three runtime surfaces backed by shared libraries:

- **PaycheckCalculator.App** — .NET MAUI app for Android, iOS, macOS (Mac Catalyst), and Windows.
- **PaycheckCalculator.Blazor** — Blazor Server web app.
- **PaycheckCalculator.API** — optional ASP.NET Core Web API for accounts and sync.

The tax, gross-up, annual projection, budgeting, and reporting engines live in the UI-agnostic `PaycheckCalculator.Core` project. Sync DTOs, JSON configuration, merge logic, store abstractions, API client code, and entitlement abstractions live in `PaycheckCalculator.Shared`.

## Features

- **Gross pay calculation** — Supports hourly pay with regular and overtime hours, plus salary pay by annual amount or per-period amount.
- **Gross-up calculator** — Solves backward from desired take-home pay to the required gross amount by repeatedly running the full paycheck pipeline. This preserves graduated brackets, FICA caps, state rules, and percentage-based deductions.
- **Bonus / supplemental-wage calculator** — Computes take-home on a one-off bonus, commission, or award using the IRS flat-rate method (22%; 37% on cumulative supplemental wages over $1,000,000) plus FICA and the selected state's supplemental rate (data-driven from `state_supplemental_2026.json`). States that publish no flat supplemental rate display a caveat that regular withholding may apply. Includes "Show Your Work" breakdowns.
- **Hourly ↔ salary converter** — Converts between an hourly rate and an annual salary given hours worked per week and paid weeks per year, then expresses the equivalent earnings across common pay frequencies. The "real hourly" direction (set hours to the hours actually worked) reveals the true effective hourly rate behind a salary.
- **Self-employment / 1099 calculator** — From annual net earnings (Schedule C net profit) it computes federal self-employment tax (15.3% — 12.4% Social Security to the $184,500 cap plus 2.9% Medicare, both halves, with 0.9% Additional Medicare over $200,000 — on 92.35% of earnings), estimates state income tax through the ordinary state engine (no state levies a separate self-employment tax), and produces a quarterly estimated-payment schedule (Form 1040-ES). It does not model federal income tax.
- **Federal income tax** — Implements the IRS Publication 15-T 2026 percentage method for automated payroll systems. Supported W-4 inputs include filing status, Step 2 checkbox, Step 3 credits, Step 4(a) other income, Step 4(b) deductions, and Step 4(c) extra withholding.
- **FICA taxes** — Calculates Social Security, Medicare, and Additional Medicare withholding, with YTD wage inputs available where needed to handle the Social Security wage-base cap and Additional Medicare threshold mid-year.
- **State withholding for all 50 states plus DC** — Each jurisdiction is handled by a registered `IStateWithholdingCalculator` under `PaycheckCalculator.Core/Tax/<StateName>/`. State-specific UI inputs are schema-driven from `PaycheckCalculator.Core/Data/Schemas/*.json`.
- **State disability / paid-leave premiums** — California SDI, Colorado FMLI, Connecticut PFMLI, and Washington WA Cares Fund are surfaced as separate result lines with dynamic labels.
- **Pre-tax and post-tax deductions** — Deductions can be dollar amounts or percentages. Pre-tax deductions independently control whether they reduce federal taxable wages, state taxable wages, and/or FICA wages.
- **Show Your Work explanations** — Result lines carry step-by-step explanations through the Core `Explanation/` model and are displayed by both front-ends.
- **Annual projection** — Projects per-paycheck results across the full year, including annualized totals, projected YTD values, estimated annual liability, and over/under withholding.
- **Multiple pay frequencies** — Daily, Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, plus 53-week and 27-biweekly payroll-calendar variants.
- **Saved paychecks and A/B comparison** — Save calculated paychecks with inputs and compare two saved results side by side. MAUI persists saved paychecks on device; Blazor keeps anonymous saved paychecks in circuit memory until the tab closes.
- **Export and print** — Both front-ends support paycheck CSV/PDF export and printing. Exports include the per-period result, annual projection, and optional A/B comparison data.
- **Monthly budget tracker** — Converts paycheck net pay to monthly income, supports Custom, 50/30/20, Zero-Based, and Envelope budgeting methods, tracks categories and transactions, and projects month-end spend.
- **Recurring bills** — Stores recurring bills with weekly, biweekly, semimonthly, monthly, quarterly, semiannual, or annual cadence and normalizes each bill to a monthly equivalent.
- **Savings goals** — Tracks target amount, current amount, optional target date, remaining amount, and required monthly contribution.
- **Budget reports and insights** — Core can generate spend-by-category history and budget-vs-actual reports. CSV/PDF report exporters exist in the front-ends. The UI currently gates reports behind `IEntitlementProvider`; the default `FreeEntitlementProvider` returns free-tier access until billing is wired.
- **Optional accounts and sync** — Email/password accounts sync paychecks, budgets, transactions, recurring bills, and savings goals across MAUI and Blazor through the Web API. Everything remains usable without an account.

## Project Structure

```text
PaycheckCalculator.slnx
├── global.json                  # .NET 11 preview SDK pin and roll-forward settings
├── PaycheckCalculator.Core/           # UI-agnostic domain, tax, pay, projection, gross-up, budget, and report engines
│   ├── Models/                  # PaycheckInput/Result, enums, UsState, Deduction, AnnualProjection, GrossUpResult, BonusInput/Result, HourlySalaryInput/Result, SelfEmploymentInput/Result
│   ├── Pay/                     # PayCalculator, PayPeriods, AnnualProjectionCalculator, GrossUpCalculator, BonusCalculator, HourlySalaryCalculator, SelfEmploymentCalculator
│   ├── Budgeting/               # Budget, categories, transactions, recurring bills, savings goals, reports
│   ├── Explanation/             # Show-your-work explanation records
│   ├── DependencyInjection/     # AddPaycheckCalculatorCore and ITaxDataReader
│   ├── Data/                    # JSON tax tables, source manifest, and dynamic state schemas
│   │   └── Schemas/             # One schema JSON file per state / DC
│   └── Tax/                     # Federal, FICA, State contracts/registry, and one folder per jurisdiction
├── PaycheckCalculator.App/            # .NET MAUI app for Android, iOS, macOS, and Windows
│   ├── Views/                   # Inputs, Results, Paychecks, Budget, Account pages
│   ├── ViewModels/              # Calculator, budget, account, saved paycheck, dynamic field VMs
│   ├── Mappers/                 # Domain-to-UI mappers
│   ├── Models/                  # UI presentation models
│   ├── Controls/                # Doughnut chart drawable
│   ├── Behaviors/               # Input formatting behavior
│   ├── Helpers/                 # Enum labels and XAML helpers
│   └── Services/                # Tax data, storage, sync, CSV, PDF, printing
├── PaycheckCalculator.Blazor/         # Blazor Server web app
│   ├── Components/Pages/        # Home, Calculator, Budget, and state landing pages
│   ├── Components/Shared/       # Shared Razor UI such as chart and explanation modal
│   ├── Services/                # Tax data, session stores, state metadata, exports, account session
│   └── wwwroot/                 # Static CSS/JS, including export.js and print styles
├── PaycheckCalculator.Shared/         # Shared sync contracts, stores, mergers, JSON, API client, entitlements
│   ├── Snapshots/               # Saved paycheck DTOs, mapper, tombstones, merger
│   ├── Budgeting/               # Budget/transaction/bill/goal DTOs, sets, tombstones, merger, sync service
│   ├── Client/                  # PaycheckApiClient, API results, tokens, base-address provider
│   ├── Entitlements/            # IEntitlementProvider and default free-tier provider
│   ├── Json/                    # Shared System.Text.Json options and converters
│   └── Sync/                    # Paycheck sync service and saved paycheck store abstraction
├── PaycheckCalculator.API/            # ASP.NET Core Web API for Identity accounts and sync
│   ├── Data/                    # SyncDbContext and EF Core entities
│   ├── Endpoints/               # Paycheck and budget sync minimal API endpoints
│   └── Migrations/              # PostgreSQL EF Core migrations
├── PaycheckCalculator.Tests/          # xUnit test suite for Core, Shared, Api, and Blazor export paths
└── docs/
    ├── class-diagram.md         # Mermaid architecture/class diagrams
    └── wiki/                    # Project wiki
```

The solution file includes six projects: `PaycheckCalculator.API`, `PaycheckCalculator.App`, `PaycheckCalculator.Blazor`, `PaycheckCalculator.Core`, `PaycheckCalculator.Shared`, and `PaycheckCalculator.Tests`.

## Technology Stack

| Component | Technology |
|---|---|
| Primary SDK | .NET 11 preview pinned in `global.json` |
| Core library | UI-agnostic `net11.0` library |
| MAUI app | .NET MAUI, `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst`, `net11.0-windows10.0.19041.0`, CommunityToolkit.Mvvm |
| Web app | ASP.NET Core Blazor Server, `net11.0` |
| Sync API | ASP.NET Core minimal APIs, ASP.NET Core Identity, EF Core, Npgsql/PostgreSQL |
| Shared contracts | `System.Text.Json` with enum, `DateOnly`, and `StateInputValues` converters |
| Tests | xUnit |
| Data | JSON tax tables and one schema JSON file per state / DC |

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) preview matching `global.json`.
- .NET MAUI workload only when building or running `PaycheckCalculator.App`:
  ```bash
  dotnet workload install maui
  ```
- Android SDK, Xcode on macOS (for iOS and Mac Catalyst), or Windows 10+ SDK for the corresponding MAUI target.
- PostgreSQL only when running `PaycheckCalculator.API` against its default production-style provider. Integration tests use an in-memory SQLite-backed test path.

`PaycheckCalculator.Core`, `PaycheckCalculator.Shared`, `PaycheckCalculator.API`, `PaycheckCalculator.Blazor`, and `PaycheckCalculator.Tests` build without the MAUI workload. `PaycheckCalculator.App` requires the MAUI workload.

## Getting Started

### Build a non-MAUI project

```bash
dotnet build PaycheckCalculator.Core
dotnet build PaycheckCalculator.Shared
dotnet build PaycheckCalculator.Blazor
dotnet build PaycheckCalculator.API
```

### Build the full solution

```bash
dotnet build PaycheckCalculator.slnx
```

The full solution build requires the MAUI workload because it includes `PaycheckCalculator.App`.

### Run tests

```bash
dotnet test PaycheckCalculator.Tests
```

The test project covers the paycheck pipeline, federal withholding, FICA, all state calculators, dynamic schemas, gross-up, annual projection, budgeting, recurring bills, savings goals, reports, snapshot JSON, merge behavior, sync API integration, and export renderers.

CI explicitly restores, builds, and tests `PaycheckCalculator.Tests` on Linux. Its project references build `PaycheckCalculator.Core`, `PaycheckCalculator.Shared`, `PaycheckCalculator.API`, and `PaycheckCalculator.Blazor` transitively; the MAUI app is not built by that workflow. CodeQL runs in a separate workflow.

### Run the Blazor web app

```bash
dotnet run --project PaycheckCalculator.Blazor
```

### Run the sync API

```bash
dotnet run --project PaycheckCalculator.API
```

The API defaults to `http://localhost:5201` and uses `ConnectionStrings:Sync` for PostgreSQL. EF Core migrations are applied at startup when the provider is PostgreSQL.

### Run the MAUI app

```bash
dotnet build PaycheckCalculator.App
```

Target-specific examples:

```bash
# Android
dotnet build PaycheckCalculator.App -t:Run -f net11.0-android

# iOS (requires macOS and Xcode)
dotnet build PaycheckCalculator.App -t:Run -f net11.0-ios

# macOS via Mac Catalyst (requires macOS and Xcode)
dotnet build PaycheckCalculator.App -t:Run -f net11.0-maccatalyst

# Windows
dotnet build PaycheckCalculator.App -t:Run -f net11.0-windows10.0.19041.0
```

## How the Paycheck Engine Works

`PayCalculator` is the main orchestrator:

1. Computes gross pay from hourly or salary inputs.
2. Resolves pre-tax and post-tax deductions.
3. Calculates FICA on FICA-taxable wages.
4. Calculates federal withholding using IRS 15-T data.
5. Delegates state withholding to `StateCalculatorRegistry`.
6. Computes net pay from gross pay minus deductions, federal tax, FICA, state tax, state disability / paid-leave premiums, and Additional Medicare.

Money values use `decimal`. Gross pay, deductions, and withholding components are rounded to cents with `MidpointRounding.AwayFromZero`; net pay is computed so the displayed equation ties out to the cent.

`GrossUpCalculator` treats `PayCalculator` as the forward function and uses bisection to solve for the required gross that produces the requested target net.

`BonusCalculator` computes take-home on a supplemental wage (bonus, commission, or award) using the IRS flat-rate method (`FederalSupplementalCalculator` — 22%; 37% on cumulative supplemental wages over $1,000,000), the same `FicaCalculator` (honoring the Social Security wage-base cap and Additional Medicare threshold), and `StateSupplementalCalculator` (data-driven from `state_supplemental_2026.json`). States that publish no flat supplemental rate set `StateUsesRegularMethod = true`.

`HourlySalaryCalculator` converts between an hourly rate and an annual salary given hours per week and paid weeks per year, then expresses the equivalent earnings across common pay frequencies. It contains no tax logic.

`SelfEmploymentCalculator` works from annual net earnings: self-employment tax is 12.4% Social Security (to the `FicaCalculator` wage base) plus 2.9% Medicare, with 0.9% Additional Medicare over the threshold, all applied to 92.35% of earnings. State income tax is estimated by running the full earnings through the same `StateCalculatorRegistry` engine at an annual frequency — there is no separate *state* self-employment tax, so the nine no-income-tax states owe $0. The result includes a quarterly estimated-payment (Form 1040-ES) schedule with federal and state splits. Federal income tax is intentionally out of scope.

`AnnualProjectionCalculator` projects the paycheck result across the full pay year and estimates annualized totals, projected YTD totals, and over/under withholding.

## State Tax Coverage

All 50 states and the District of Columbia are supported through `IStateWithholdingCalculator` implementations registered in `AddPaycheckCalculatorCore`.

| Category | Jurisdictions |
|---|---|
| Shared no-income-tax adapter | AK, FL, NV, NH, SD, TN, TX |
| Dedicated no-income-tax calculators | WA, WY |
| Dedicated income-tax calculators | AL, AZ, AR, CA, CO, CT, DC, DE, GA, HI, IA, ID, IL, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, ND, NE, NJ, NM, NY, OH, OK, OR, PA, RI, SC, UT, VA, VT, WI, WV |

Several calculators are JSON-backed: IRS 15-T, Arkansas, California, Colorado, Connecticut, and Oklahoma. All jurisdictions have schema files in `PaycheckCalculator.Core/Data/Schemas/` for dynamic state inputs.

## Budgeting, Reports, and Sync

The budgeting engine lives in `PaycheckCalculator.Core/Budgeting/` and includes:

- `Budget`, `BudgetCategory`, `BudgetTransaction`, and `BudgetCalculator`.
- `BudgetMethod` templates for Custom, 50/30/20, Zero-Based, and Envelope budgets.
- `RecurringBill` and `RecurrencePeriods` for monthly-equivalent bill normalization.
- `SavingsGoal` for target-date contribution planning.
- `BudgetReportCalculator` for spend-by-category and budget-vs-actual reporting.

The shared sync layer stores budgets, transactions, recurring bills, and savings goals as separate sets with tombstones. Budgets are keyed by case-insensitive name; transactions, recurring bills, and savings goals are keyed by stable GUID. `BudgetMerger` applies deterministic last-write-wins rules across all four collections.

The API persists those sets in PostgreSQL through `BudgetEntity`, `BudgetTransactionEntity`, `RecurringBillEntity`, and `SavingsGoalEntity` rows.

## Documentation

- [Roadmap](ROADMAP.md) — Prioritized product milestones, engineering improvements, feature recommendations, and release criteria.
- [Wiki](docs/wiki/Home.md) — Architecture, getting started, tax engine, state coverage, budgeting, accounts/sync, UI guide, and contributing notes.
- [Accuracy & Source Governance](docs/wiki/Accuracy-and-Source-Governance.md) — Official-source manifest rules and the accuracy-incident correction process.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid diagrams for the solution, core pipeline, MAUI layer, shared sync layer, and API persistence model.
