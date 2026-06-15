# Architecture

PaycheckCalc keeps a clean separation between UI and business logic. Two front-ends — a **.NET MAUI** app
(MVVM with CommunityToolkit.Mvvm) and a **Blazor Server** web app — delegate all tax, projection, gross-up,
and budgeting math to the UI-agnostic `PaycheckCalc.Core` library. Optional account/sync concerns live in
`PaycheckCalc.Shared` (contracts) and `PaycheckCalc.Api` (the HTTP server).

---

## Solution Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.Core/             # Business logic (no UI / HTTP / persistence dependencies)
│   ├── Models/                    # PaycheckInput/Result, Deduction, enums, UsState, AnnualProjection, GrossUpResult
│   ├── Pay/                       # PayCalculator, PayPeriods, AnnualProjectionCalculator, GrossUpCalculator
│   ├── Budgeting/                 # Budget engine: Budget, BudgetCategory, BudgetTransaction, BudgetCalculator,
│   │                              #   BudgetSummary, AllocationRules (50/30/20), MonthlyIncomeNormalizer
│   ├── Explanation/               # "Show Your Work" step-by-step breakdowns
│   ├── DependencyInjection/       # AddPaycheckCalcCore (registry + calculator wiring) + ITaxDataReader
│   ├── Data/                      # JSON tax bracket tables (federal + 5 states) and Schemas/*.json
│   └── Tax/
│       ├── Federal/               # IRS 15-T percentage calculator
│       ├── Fica/                  # Social Security & Medicare
│       ├── State/                 # State tax interfaces, registry, schema provider, adapters, shared steps
│       └── <StateName>/           # One folder per state (50 + DC), each with a dedicated calculator
├── PaycheckCalc.App/              # .NET MAUI frontend (Android & Windows), MVVM
│   ├── Views/ ViewModels/ Mappers/ Models/ Controls/ Behaviors/ Helpers/
│   └── Services/                  # Tax data reader, Pdf/Csv/Printing exporters, Storage, Sync/Auth
├── PaycheckCalc.Blazor/           # Blazor Server frontend (web)
│   ├── Components/Pages/          # Home, Calculator, Budget, StateLandingPage (per-state SEO)
│   ├── Components/Shared/         # DoughnutChart (SVG), ExplanationModal
│   └── Services/                  # FileSystemTaxDataReader, session stores, StateMetadata, Export/
├── PaycheckCalc.Shared/           # Sync contracts, JSON, merge, HTTP client, store abstractions
│   ├── Snapshots/                 # SavedPaycheckDto/ResultDto/Mapper/Tombstone/Set + SavedPaycheckMerger
│   ├── Budgeting/                 # BudgetDto/CategoryDto/TransactionDto, sets, tombstones, BudgetMerger, IBudgetStore
│   ├── Json/                      # PaycheckJson + StateInputValues/DateOnly converters
│   ├── Client/                    # PaycheckApiClient, ApiResult, AuthTokens, ITokenStore, IApiBaseAddressProvider
│   └── Sync/                      # ISavedPaycheckStore, PaycheckSyncService, sync contracts
├── PaycheckCalc.Api/              # ASP.NET Core Web API: Identity accounts + sync (PostgreSQL/EF Core)
│   ├── Data/                      # SyncDbContext (IdentityDbContext) + SavedPaycheck/Budget/Transaction entities
│   ├── Endpoints/                 # PaycheckSyncEndpoints, BudgetSyncEndpoints (authorized minimal-API groups)
│   └── Migrations/                # EF Core migrations (InitialCreate, AddBudgets)
└── PaycheckCalc.Tests/            # xUnit suite (Core + Shared + Api + Blazor)
```

`PaycheckCalc.Shared`, `PaycheckCalc.Api`, `PaycheckCalc.Blazor`, and `PaycheckCalc.Tests` are `net11.0`
and build without the MAUI workload; `PaycheckCalc.Core` multi-targets `net11.0;net9.0`. See
**[Accounts & Sync](Accounts-and-Sync.md)** for the account/sync design and **[Budgeting](Budgeting.md)**
for the budget engine.

---

## Key Architectural Principles

1. **Core stays UI-agnostic.** `PaycheckCalc.Core` has zero dependency on MAUI, XAML, HTTP, or any UI
   framework. All tax, projection, gross-up, and budgeting math and models live here.

2. **PayCalculator is an orchestrator.** It composes gross pay, deductions, FICA, federal withholding,
   state withholding, and net pay — but does not contain state-specific branching or tax rules.

3. **State calculators are plugins.** Each of the 50 states + DC implements `IStateWithholdingCalculator`
   and is registered in `StateCalculatorRegistry`. The registry is built at startup in
   `AddPaycheckCalcCore` (`PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`),
   which both `MauiProgram.cs` and the Blazor `Program.cs` call with a platform `ITaxDataReader`.

4. **Schema-driven state UI.** State-specific input fields are not hard-coded in XAML/Razor. Each state
   calculator declares its input schema via `GetInputSchema()` (backed by `JsonStateSchemaProvider` over
   `Data/Schemas/*.json`), and both UIs render fields dynamically.

5. **`decimal` everywhere for money.** All monetary values, rates, thresholds, and deductions use
   `decimal`. No `double`/`float` in calculation code.

6. **Sync stays out of Core.** Account/sync wire and storage concerns live in `PaycheckCalc.Shared`; the
   HTTP server lives in `PaycheckCalc.Api`. Core remains HTTP- and persistence-free, and
   `PaycheckCalc.Api` never references the front-end projects.

---

## Front-ends

Both front-ends call `AddPaycheckCalcCore`, supplying a platform tax-data reader:

- **MAUI** (`PaycheckCalc.App`) — a five-tab Shell (Inputs, Results, Paychecks, Budget, Account). Pages are
  thin; the shared `CalculatorViewModel` owns calculator state, and `BudgetViewModel`/`AccountViewModel`
  own their tabs. **Mappers** translate between domain types and presentation models. Tax JSON is read
  from the app package via `MauiAppPackageTaxDataReader`. See [UI Guide](UI-Guide.md).
- **Blazor** (`PaycheckCalc.Blazor`) — an interactive Server-rendered calculator page, a `/budget` page,
  per-state SEO landing pages (`/{state}-paycheck-calculator` via `StateMetadata`), a sitemap, and a
  "Saved Paychecks & Account" panel. Tax JSON is read from a `TaxData/` folder in the build output via
  `FileSystemTaxDataReader`.

---

## Accounts, Sync & Budgeting

Saved **paychecks** and **budgets** can optionally sync across the two front-ends via a user account. The
shared mergers (`SavedPaycheckMerger`, `BudgetMerger` — deterministic last-write-wins, paychecks/budgets
keyed by case-insensitive name and transactions by GUID) run server-side and are reused by both clients and
the tests. The `ISavedPaycheckStore` and `IBudgetStore` abstractions each have implementations for on-device
JSON (MAUI), circuit memory (Blazor, so anonymous data dies with the tab), and EF Core PostgreSQL rows
(server). Full design in **[Accounts & Sync](Accounts-and-Sync.md)**.

---

## MVVM Data Flow (MAUI)

```
User Input (InputsPage)
    ↓
CalculatorViewModel  ←  builds PaycheckInput from UI state (PaycheckInputMapper)
    ↓
PayCalculator.Calculate(input)  →  PaycheckResult        (or GrossUpCalculator for gross-up mode)
    ↓
ResultCardMapper  →  ResultCardModel (UI presentation)
AnnualProjectionCalculator.Calculate(input, result)  →  AnnualProjectionMapper  →  AnnualProjectionModel
    ↓
ResultsPage (XAML binding)
```

### View Models

| View Model | Responsibility |
|---|---|
| `CalculatorViewModel` | Central VM shared by the Inputs and Results pages: input state, standard/gross-up calculation, result/projection mapping, exports, and "Show Your Work" explanations. |
| `StateFieldViewModel` | Wraps a single `StateFieldDefinition` for dynamic state input rendering. |
| `DeductionItemViewModel` | Wraps a single deduction entry. |
| `SavedPaycheckViewModel` | Wraps a saved paycheck (name + result card + snapshot) on the Paychecks page. |
| `BudgetViewModel` / `BudgetCategoryViewModel` / `BudgetTransactionViewModel` | Own the Budget tab: categories, transactions, and summaries. |
| `AccountViewModel` | Sign-in/out, server URL, and sync status on the Account tab. |

### Mappers

| Mapper | From → To |
|---|---|
| `PaycheckInputMapper` | UI state → `PaycheckInput` |
| `ResultCardMapper` | `PaycheckResult` → `ResultCardModel` (standard and gross-up) |
| `AnnualProjectionMapper` | `AnnualProjection` → `AnnualProjectionModel` |
| `SavedPaycheckSnapshotMapper` | `ResultCardModel` + `PaycheckInput` ↔ `SavedPaycheckDto` |

---

## Dependency Injection

Core services are registered by `AddPaycheckCalcCore`, called from
[`MauiProgram.cs`](../../PaycheckCalc.App/MauiProgram.cs) and the Blazor
[`Program.cs`](../../PaycheckCalc.Blazor/Program.cs).

### Core Services

- `FicaCalculator` — Social Security and Medicare computation.
- `Irs15TPercentageCalculator` — Federal income tax (loaded from JSON at startup).
- `StateCalculatorRegistry` — Central registry of all 51 state calculators.
- `IStateSchemaProvider` → `JsonStateSchemaProvider` — per-state input schemas from `Data/Schemas/*.json`.
- `PayCalculator` — Main paycheck calculation orchestrator; consumes the state registry.
- `AnnualProjectionCalculator` — Annualized totals, projected YTD, and year-end over/under estimate.
- `GrossUpCalculator` — Inverse solver: bisects on gross to deliver a target net.
- `BudgetCalculator` — Monthly budget summary and month-end projection.

### JSON-Backed Calculators

- `Irs15TPercentageCalculator` ← `us_irs_15t_2026_percentage_automated.json`
- `ArkansasFormulaCalculator` ← `ar_withholding_2026.json`
- `OklahomaOw2PercentageCalculator` ← `ok_ow2_2026_percentage.json`
- `CaliforniaPercentageCalculator` ← `ca_method_b_2026.json`
- `ColoradoWithholdingCalculator` ← `co_dr0004_2026.json`
- `ConnecticutWithholdingCalculator` ← `connecticut_withholding_2026.json`

Each per-state wrapper (`ArkansasWithholdingCalculator`, `CaliforniaWithholdingCalculator`,
`OklahomaWithholdingCalculator`) composes the corresponding JSON-backed calculator. Every other state has a
dedicated calculator; the generic `PercentageMethodWithholdingAdapter` (driven by `StateTaxConfigs2026`,
now empty) is retained only for tests.

### Front-end registrations

The MAUI app additionally registers its pages and view models (singletons), the PDF/CSV/print export
services, the on-device stores (`JsonFilePaycheckStore`, `JsonFileBudgetStore`), and the sync/auth services
(`PaycheckApiClient`, `SecureStorageTokenStore`, `PreferencesApiBaseAddressProvider`, `SyncCoordinator`).
The Blazor app registers interactive server components, the circuit-scoped session stores
(`SessionPaycheckStore`, `SessionBudgetStore`), `CircuitAccountSession`, and `ConfigApiBaseAddressProvider`.

---

## Class Diagram

A Mermaid UML class diagram is available at [`docs/class-diagram.md`](../class-diagram.md).
