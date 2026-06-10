# Architecture

PaycheckCalc follows a clean separation between UI and business logic. The MAUI app uses MVVM with CommunityToolkit.Mvvm and delegates all tax math to the UI-agnostic `PaycheckCalc.Core` library.

---

## Solution Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/              # .NET MAUI frontend (Android & Windows)
│   ├── Views/                     # XAML pages (InputsPage, ResultsPage)
│   ├── ViewModels/                # MVVM view models
│   ├── Mappers/                   # Domain ↔ UI translation
│   ├── Models/                    # UI presentation models
│   ├── Controls/                  # Custom controls (e.g., DoughnutChartDrawable)
│   ├── Behaviors/                 # Input formatting behaviors
│   ├── Helpers/                   # Enum display helpers and converters
│   ├── Services/                  # MauiAppPackageTaxDataReader (tax JSON loading)
│   └── MauiProgram.cs             # DI configuration & app startup
├── PaycheckCalc.Core/             # Business logic (no UI dependencies)
│   ├── Models/                    # Domain models and enums
│   ├── Pay/                       # PayCalculator, AnnualProjectionCalculator
│   ├── Explanation/               # "Show Your Work" step-by-step breakdowns
│   ├── DependencyInjection/       # AddPaycheckCalcCore (registry + calculator wiring)
│   ├── Data/                      # JSON tax bracket tables (state + federal)
│   └── Tax/
│       ├── Federal/               # IRS 15-T percentage calculator
│       ├── Fica/                  # Social Security & Medicare
│       ├── State/                 # State tax interfaces, registry, generic percentage-method adapter
│       └── <StateName>/           # One folder per state, each with a dedicated calculator
└── PaycheckCalc.Tests/            # xUnit test suite
```

---

## Key Architectural Principles

1. **Core stays UI-agnostic.** `PaycheckCalc.Core` has zero dependency on MAUI, XAML, or any UI framework. All tax math and models live here.

2. **PayCalculator is an orchestrator.** It composes gross pay, deductions, FICA, federal withholding, state withholding, and net pay — but does not contain state-specific branching or tax rules.

3. **State calculators are plugins.** Each state implements `IStateWithholdingCalculator` and is registered in `StateCalculatorRegistry`. The registry is built at startup in `AddPaycheckCalcCore` (`PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`), which `MauiProgram.cs` calls with a `MauiAppPackageTaxDataReader`.

4. **Schema-driven state UI.** State-specific input fields (filing status, allowances, dependents, etc.) are not hard-coded in XAML. Instead, each state calculator declares its input schema via `GetInputSchema()`, and the UI renders fields dynamically.

5. **`decimal` everywhere for money.** All monetary values, tax rates, thresholds, and deductions use `decimal`. No `double` or `float` in calculation code.

---

## MVVM Data Flow

```
User Input (InputsPage)
    ↓
CalculatorViewModel  ←  builds PaycheckInput from UI state
    ↓
PayCalculator.Calculate(input)  →  PaycheckResult
    ↓
ResultCardMapper  →  ResultCardModel (UI presentation)
AnnualProjectionCalculator.Calculate(input, result)  →  AnnualProjectionMapper  →  AnnualProjectionModel
    ↓
ResultsPage (XAML binding)
```

### View Models

| View Model | Responsibility |
|---|---|
| `CalculatorViewModel` | Central VM shared by the Inputs and Results pages. Owns all input state, triggers calculation, maps results, and serves "Show Your Work" explanations. |
| `StateFieldViewModel` | Wraps a single `StateFieldDefinition` for dynamic state input rendering. |
| `DeductionItemViewModel` | Wraps a single deduction entry for the deductions list. |

### Mappers

Mappers translate between domain models and UI presentation models:

| Mapper | From → To |
|---|---|
| `PaycheckInputMapper` | UI state → `PaycheckInput` |
| `ResultCardMapper` | `PaycheckResult` → `ResultCardModel` |
| `AnnualProjectionMapper` | `AnnualProjection` → `AnnualProjectionModel` |

---

## Dependency Injection

Core services are registered by `AddPaycheckCalcCore`, called from [`MauiProgram.cs`](../../PaycheckCalc.App/MauiProgram.cs).

### Core Services

- `FicaCalculator` — Social Security and Medicare computation.
- `Irs15TPercentageCalculator` — Federal income tax (loaded from JSON at startup).
- `StateCalculatorRegistry` — Central registry of all 51 state calculators.
- `PayCalculator` — Main paycheck calculation orchestrator; consumes the state registry.
- `AnnualProjectionCalculator` — Annualized totals, projected year-to-date amounts, and the year-end over/under withholding estimate.
- `IStateSchemaProvider` → `JsonStateSchemaProvider` — per-state input schemas loaded from `Data/Schemas/*.json`.

### JSON-Backed Calculators

Several calculators load their tax tables from JSON at startup:

- `ArkansasFormulaCalculator` ← `ar_withholding_2026.json`
- `OklahomaOw2PercentageCalculator` ← `ok_ow2_2026_percentage.json`
- `CaliforniaPercentageCalculator` ← `ca_method_b_2026.json`
- `ColoradoWithholdingCalculator` ← `co_dr0004_2026.json`
- `ConnecticutWithholdingCalculator` ← `connecticut_withholding_2026.json`
- `Irs15TPercentageCalculator` ← `us_irs_15t_2026_percentage_automated.json`

### Pages and View Models

The two pages (`InputsPage`, `ResultsPage`), the shared `CalculatorViewModel`, and `AppShell` are registered as singletons in `MauiProgram.cs` and resolved via Shell `DataTemplate` bindings.

---

## Class Diagram

A Mermaid UML class diagram is available at [`docs/class-diagram.md`](../class-diagram.md).
