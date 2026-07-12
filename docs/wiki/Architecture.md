# Architecture

PaycheckCalc keeps a strict separation between domain logic, front-end presentation, shared sync contracts, and the optional sync API.

The current solution has six projects:

- `PaycheckCalculator.Core` — UI-agnostic paycheck, tax, gross-up, annual projection, budgeting, and budget-report engines.
- `PaycheckCalculator.App` — .NET MAUI frontend for Android and Windows.
- `PaycheckCalculator.Blazor` — Blazor Server web frontend.
- `PaycheckCalculator.Shared` — sync DTOs, JSON options, deterministic mergers, HTTP client, store abstractions, and entitlement abstractions.
- `PaycheckCalculator.Api` — ASP.NET Core Web API for optional accounts and sync.
- `PaycheckCalculator.Tests` — xUnit test suite.

---

## Solution Structure

```text
PaycheckCalculator.slnx
├── PaycheckCalculator.Core/             # Business logic; no UI / HTTP / persistence dependencies
│   ├── Models/                    # PaycheckInput/Result, Deduction, enums, UsState, AnnualProjection, GrossUpResult, SelfEmploymentInput/Result
│   ├── Pay/                       # PayCalculator, PayPeriods, AnnualProjectionCalculator, GrossUpCalculator, SelfEmploymentCalculator
│   ├── Budgeting/                 # Budget engine, recurring bills, savings goals, reports
│   ├── Explanation/               # Show-your-work breakdowns
│   ├── DependencyInjection/       # AddPaycheckCalcCore + ITaxDataReader
│   ├── Data/                      # Federal/state tax JSON and Schemas/*.json
│   └── Tax/                       # Federal, FICA, State contracts/registry, and dedicated jurisdiction calculators
├── PaycheckCalculator.App/              # MAUI frontend
├── PaycheckCalculator.Blazor/           # Blazor Server frontend
├── PaycheckCalculator.Shared/           # Shared contracts and client sync infrastructure
├── PaycheckCalculator.Api/              # ASP.NET Core Identity + sync API
└── PaycheckCalculator.Tests/            # xUnit suite
```

---

## Key Architectural Principles

1. **Core stays UI-agnostic.** `PaycheckCalculator.Core` contains math, tax rules, domain models, explanations, and budget/report engines. It does not reference MAUI, Blazor, HTTP, EF Core, file-system persistence, or platform APIs.

2. **PayCalculator is an orchestrator.** It composes gross pay, deductions, FICA, federal withholding, state withholding, state disability / paid-leave premiums, and net pay. It does not contain state-specific tax logic.

3. **State calculators are plugins.** Every state and DC implements `IStateWithholdingCalculator` and is registered in `StateCalculatorRegistry` by `AddPaycheckCalcCore`.

4. **State UI is schema-driven.** State-specific fields are declared by state schema JSON and surfaced through `StateFieldDefinition`. Both front-ends render those fields dynamically instead of hard-coding per-state controls.

5. **Money uses `decimal`.** Monetary values, rates, thresholds, deductions, and tax outputs use `decimal`; calculation paths should not introduce `double` or `float`.

6. **Sync stays outside Core.** Sync DTOs, API client code, JSON options, and merge rules live in `PaycheckCalculator.Shared`. HTTP endpoints and EF Core persistence live in `PaycheckCalculator.Api`.

7. **Mergers are deterministic.** Saved paychecks, budgets, transactions, recurring bills, and savings goals use last-write-wins merge rules with deletion markers so removals propagate.

---

## Dependency Graph

```text
PaycheckCalculator.Core
   ↑
PaycheckCalculator.Shared
   ↑
PaycheckCalculator.Api

PaycheckCalculator.App     → PaycheckCalculator.Core + PaycheckCalculator.Shared
PaycheckCalculator.Blazor  → PaycheckCalculator.Core + PaycheckCalculator.Shared
PaycheckCalculator.Tests   → Core + Shared + Api + Blazor
```

`PaycheckCalculator.App` and `PaycheckCalculator.Blazor` communicate with `PaycheckCalculator.Api` over HTTP through `PaycheckApiClient`; they do not reference the API project directly.

---

## Core Engine

`AddPaycheckCalcCore` is the composition root for Core. It:

- Reads IRS 15-T and state tax JSON through `ITaxDataReader`.
- Reads every available `schemas/<state>.json` file into `JsonStateSchemaProvider`.
- Registers FICA, federal, state, pay, gross-up, annual projection, budget, and budget-report calculators.
- Builds the `StateCalculatorRegistry` and registers all 50 states plus DC.

Important Core services:

| Service | Role |
|---|---|
| `PayCalculator` | Main paycheck calculation orchestrator |
| `FicaCalculator` | Social Security, Medicare, Additional Medicare |
| `Irs15TPercentageCalculator` | Federal withholding using IRS 15-T data |
| `StateCalculatorRegistry` | Maps `UsState` to `IStateWithholdingCalculator` |
| `AnnualProjectionCalculator` | Full-year annualization and over/under estimate |
| `GrossUpCalculator` | Inverse solver for target net pay |
| `BonusCalculator` | Supplemental-wage (bonus) take-home using flat-rate federal + FICA + state supplemental rates |
| `HourlySalaryCalculator` | Pure hourly ↔ salary rate converter across pay frequencies |
| `SelfEmploymentCalculator` | Self-employment (1099) tax, state income-tax estimate, and quarterly estimated payments |
| `BudgetCalculator` | Monthly budget summary with categories, transactions, recurring bills, and savings goals |
| `BudgetReportCalculator` | Spend-by-category and budget-vs-actual report data |

---

## MAUI Frontend

`PaycheckCalculator.App` uses .NET MAUI Shell and MVVM with CommunityToolkit.Mvvm.

Main tabs:

| Tab | Page | Responsibility |
|---|---|---|
| Inputs | `InputsPage` | Pay, federal, state, and deduction inputs |
| Results | `ResultsPage` | Per-paycheck and annual results, chart, explanations, export/print |
| Paychecks | `PaychecksPage` | Saved paycheck list and A/B comparison |
| Budget | `BudgetPage` | Budget methods, categories, transactions, recurring bills, savings goals, reports |
| Account | `AccountPage` | Optional sign-in, sync, sign-out, and server URL |

`CalculatorViewModel` is shared by the Inputs, Results, and Paychecks pages. `BudgetViewModel` owns budget state and uses `IBudgetStore`, `BudgetCalculator`, `BudgetReportCalculator`, `IEntitlementProvider`, and CSV export services. `AccountViewModel` owns account and sync state.

The MAUI app reads tax JSON from package assets through `MauiAppPackageTaxDataReader`, persists saved paychecks and budgets on device, and syncs through `SyncCoordinator`.

---

## Blazor Frontend

`PaycheckCalculator.Blazor` is an interactive Blazor Server app. It includes:

- Home page and state SEO landing pages.
- Calculator page with inputs, per-paycheck results, annual projection, saved paychecks/account panel, exports, and print support.
- Budget page with income normalization, budgeting methods, category bars, recurring bills, savings goals, expenses, and Pro-gated reports.
- Session stores for anonymous saved paychecks and budgets.

The Blazor app reads tax JSON from a `TaxData/` build-output folder using `FileSystemTaxDataReader`. Account and sync calls are made server-side from the Blazor circuit.

---

## Budgeting, Reports, and Entitlements

Budgeting lives in `PaycheckCalculator.Core/Budgeting/`.

Core models include:

- `Budget`, `BudgetCategory`, `BudgetTransaction`, and `BudgetSummary`.
- `BudgetMethod`: `Custom`, `FiftyThirtyTwenty`, `ZeroBased`, and `Envelope`.
- `RecurringBill`, `RecurrenceFrequency`, and `RecurrencePeriods` for monthly-equivalent bills.
- `SavingsGoal` for target amount, current amount, optional target date, and monthly contribution needed.
- `BudgetReport`, `SpendByCategoryPoint`, `BudgetVsActualPoint`, and `BudgetReportCalculator`.

`IEntitlementProvider` in Shared reports whether Pro features are available. The current default provider is `FreeEntitlementProvider`, so report UI is present but gated until a paid entitlement implementation is wired.

---

## Accounts and Sync

`PaycheckCalculator.Shared` owns the portable sync model:

- Saved paycheck snapshots and deletion markers.
- Budget DTOs and deletion markers.
- Transaction DTOs and deletion markers.
- Recurring bill DTOs and deletion markers.
- Savings goal DTOs and deletion markers.
- Deterministic last-write-wins mergers.
- `PaycheckApiClient`.
- Store abstractions used by MAUI, Blazor, and sync orchestration.

`PaycheckCalculator.Api` exposes account endpoints through ASP.NET Core Identity and authorized sync endpoints for paychecks and budgets. The budget sync endpoint merges four independent collections: budgets, transactions, recurring bills, and savings goals.

The server persists sync state through `SyncDbContext` using PostgreSQL entities:

- `SavedPaycheckEntity`
- `BudgetEntity`
- `BudgetTransactionEntity`
- `RecurringBillEntity`
- `SavingsGoalEntity`

---

## Class Diagram

A Mermaid UML class diagram is available at [`docs/class-diagram.md`](../class-diagram.md).
