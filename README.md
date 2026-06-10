# PaycheckCalc

A cross-platform paycheck calculator that computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. The solution ships with two front-ends — a **.NET MAUI** app (Android & Windows) and a **Blazor Server** web head — both backed by a shared UI-agnostic core library. It also includes self-employment (Schedule SE + QBI) and annual Form 1040 tax estimation modules.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000).
- **State Income Tax** — Covers all 50 states and DC. Every state ships its own `IStateWithholdingCalculator` implementation under `PaycheckCalc.Core/Tax/<State>/`, registered centrally via `StateCalculatorRegistry`:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY. Most use the shared `NoIncomeTaxWithholdingAdapter`; WA has a dedicated calculator for the WA Cares Fund (0.58% LTC premium with opt-out), and WY has its own dedicated (empty-schema) calculator.
  - **Flat-rate state** — Pennsylvania (3.07%).
  - **All other states** — Each implements its own withholding rules (W-4 / state-specific certificate, standard deductions, allowances / exemptions, graduated brackets or flat rate, and state-specific credits). Examples include Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables + SDI), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Connecticut (TPG-211 table-driven withholding + PFMLI), Delaware (DE W-4, 7 graduated brackets + $110 personal credit), Georgia (flat 5.19% per HB 111 + G-4 allowances and dependent deductions), Illinois (flat 4.95% + IL-W-4 allowances), Ohio (IT-4 exemptions + two-bracket formula), Oklahoma (OW-2), Oregon (OR-W-4 with per-allowance tax credit), Utah (flat 4.5% with phase-out allowance credit), Virginia (VA-4), Wisconsin (WT-4), West Virginia (IT-104), and the remaining states that use an annualized graduated-bracket approach with state-specific deductions and allowances.
- **Local / Sub-State Taxes** — Plugin-based local (city / county / school district) withholding with calculators for Pennsylvania Act 32 EIT + LST, New York City, Ohio RITA and CCA, and Maryland county surtax, all JSON-backed.
- **State Disability / Family Leave Insurance** — California SDI, Colorado FMLI (0.044%), Connecticut PFMLI, and the Washington WA Cares Fund (0.58% LTC premium) are computed alongside state withholding with dynamic labels on the results screen and exports.
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions that reduce taxable wages.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **Annual Projection** — Estimates annualized gross, taxes, and net pay; tracks current paycheck number and remaining pay periods, and projects year-end over/under withholding.
- **Self-Employment Tax** — Schedule SE calculator with FICA coordination for W-2 wages (reduces remaining Social Security wage base and Additional Medicare threshold) plus Form 8995/8995-A QBI deduction.
- **Annual Form 1040 Estimation** — Orchestrates full-year federal tax estimates using 2026 Rev. Proc. 2025-32 brackets and standard deductions, Schedule 1 adjustments, Child Tax Credit, Form 8863 education credits, Form 8880 saver's credit, and Form 8960 Net Investment Income Tax.
- **Quarterly Estimated Taxes** — Form 1040-ES quarterly estimate calculator and a per-period withholding-suggestion calculator for closing projected year-end balances.
- **Address & Jurisdiction Lookup** — Optional Google Maps geocoding + jurisdiction resolver maps a home/work address to the applicable state and local tax jurisdictions.
- **Export Results** — Exports per-period paycheck results and self-employment / Schedule SE results to CSV or PDF (PDF powered by QuestPDF) and shares via the native OS share sheet.
- **Results Visualization** — Doughnut chart breakdown of gross pay by category (federal tax, state tax, Social Security, Medicare, net pay).
- **Side-by-Side Comparison** — Save a snapshot of one calculation and compare it against the current inputs.
- **Saved Paychecks** — Persist named paycheck calculations to local JSON (MAUI) or browser `localStorage` (Blazor), reload them into the calculator, and manage saved entries from a dedicated page.
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Dashboard, Inputs and its per-tab pages
│   │                          #   PayHours/Federal/State/Deductions, Results, Compare,
│   │                          #   SavedPaychecks, SelfEmployment, SelfEmploymentResults,
│   │                          #   AnnualProjection, JobsAndYtd, OtherIncomeAdjustments,
│   │                          #   Credits, QuarterlyEstimates, WhatIf, AnnualTaxResults)
│   ├── ViewModels/            # MVVM view models (Dashboard, Calculator, Compare, SavedPaycheck,
│   │                          #   SavedPaychecks, SelfEmployment, AnnualTax, AnnualProjection,
│   │                          #   JobsAndYtd, W2JobItem, OtherIncomeAdjustments, Credits,
│   │                          #   QuarterlyEstimates, WhatIf, StateField, DeductionItem)
│   ├── Mappers/               # Domain-to-UI mappers (ResultCard, AnnualProjection, Scenario,
│   │                          #   SavedPaycheck, PaycheckInput, PaycheckInputRestorer,
│   │                          #   AnnualTaxInput, AnnualTaxResult, AnnualScenario,
│   │                          #   JobsAndYtd, OtherIncomeAdjustments, Credits,
│   │                          #   QuarterlyEstimatesInput, QuarterlyEstimatesResult,
│   │                          #   SelfEmploymentInput, SelfEmploymentResult)
│   ├── Models/                # UI presentation models (ResultCardModel, AnnualProjectionModel,
│   │                          #   AnnualTaxResultModel, ScenarioSnapshot, JobsYtdSummaryModel,
│   │                          #   QuarterlyEstimatesCardModel, SelfEmploymentResultModel,
│   │                          #   WhatIfComparisonModel, SavedAnnualScenarioItemViewModel)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers and XAML value converters
│   │                          #   (GreaterThanZero, InvertBool, NonEmptyString)
│   ├── Services/              # AnnualTaxSession, ComparisonSession, geocoding/jurisdiction services
│   ├── Storage/               # JsonPaycheckRepository, JsonAnnualScenarioRepository (local JSON persistence)
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Blazor/       # Blazor Server (Blazor Web App, net10.0) web head
│   ├── Components/            # Razor components and pages (Home, Inputs, Results,
│   │                          #   SavedPaychecks, SelfEmployment, SelfEmploymentResults)
│   ├── Services/              # CalculatorSessionState, SelfEmploymentSessionState
│   │                          #   (per-circuit state) + LocalStoragePaycheckRepository
│   │                          #   (browser localStorage IPaycheckRepository)
│   ├── wwwroot/               # Static assets; tax JSON content-linked from Core/Data at build
│   │                          #   plus js/paycheckStorage.js JS interop shim
│   └── Program.cs             # Web host, DI, and tax-data loading
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput/Result, Enums, Deduction, AnnualProjection,
│   │                          #   CalculationScenario, SavedPaycheck, SelfEmploymentInput,
│   │                          #   TaxYearProfile, AnnualTaxResult, W2JobInput, Credits & other-income inputs,
│   │                          #   WithholdingSuggestion*, QuarterlyEstimatesResult, SavedAnnualScenario
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator,
│   │                          #   YtdSummaryCalculator
│   ├── Explanation/           # "Show Your Work" engine (PaycheckExplanation, LineExplanation,
│   │                          #   ExplanationStep, ExplanationLineKey) — step-by-step
│   │                          #   walkthroughs of every line on a paycheck
│   ├── Export/                # CsvPaycheckExporter, PdfPaycheckExporter (QuestPDF),
│   │                          #   CsvSelfEmploymentExporter, PdfSelfEmploymentExporter (QuestPDF)
│   ├── Geocoding/             # Address input, IGeocodingService, GeocodeResult
│   ├── Storage/               # IPaycheckRepository, IAnnualScenarioRepository
│   ├── Data/                  # JSON tax tables (IRS 15-T, Federal 1040, OK OW-2, CA Method B, AR,
│   │                          #   CO DR 0004, CT TPG-211, PA EIT, NYC, OH RITA/CCA, MD county surtax)
│   │   └── Schemas/           # One JSON file per state (al.json … wy.json) declaring that
│   │                          #   state's input schema for the schema-driven UI
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       │   └── Annual/        # Form 1040 orchestrator, Federal 1040 brackets, Schedule 1,
│       │                      #   CTC, Form 8863/8880/8960, Form 1040-ES, Withholding suggestion
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── SelfEmployment/    # Schedule SE tax & Form 8995/8995-A QBI deduction
│       ├── State/             # State tax interfaces, registry, generic percentage-method adapter,
│       │                      #   no-income-tax adapter, and annual state-tax calculator
│       ├── Local/             # Local (city/county/school district) plugin model + registry
│       │   ├── Maryland/      # County surtax (JSON-backed)
│       │   ├── NewYork/       # NYC withholding (JSON-backed)
│       │   ├── Ohio/          # OhioMunicipalCalculator covering RITA + CCA (JSON-backed)
│       │   └── Pennsylvania/  # Act 32 EIT (JSON-backed) + LST flat head tax
│       └── <State>/           # One folder per state (Alabama, Alaska, Arizona, … Wyoming)
│                              #   each containing a dedicated IStateWithholdingCalculator
│                              #   implementation for that state's withholding rules
├── PaycheckCalc.Tests/        # xUnit test suite
│   └── Local/                 # Local-tax calculator and geocoding integration tests
└── docs/                      # Class diagrams and documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| **Frameworks** | .NET 10 — MAUI (PaycheckCalc.App) and Blazor Web App / Server rendering (PaycheckCalc.Blazor) |
| **Target Platforms** | Android, Windows 10+ (MAUI); modern browsers via server-rendered Blazor |
| **UI Pattern** | MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) on MAUI; Razor components on Blazor |
| **Test Framework** | xUnit 2.9.3 |
| **PDF Export** | [QuestPDF](https://www.questpdf.com/) 2026.5.0 |
| **Tax Data** | JSON-based IRS 15-T, Federal 1040, and state / local tax bracket tables (2026) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) — the solution targets `net10.0` (see `global.json` for the pinned SDK version and roll-forward settings)
- .NET MAUI workload (only required for the MAUI App project):
  ```bash
  dotnet workload install maui
  ```

## Getting Started

### Build the Core Library

```bash
dotnet build PaycheckCalc.Core
```

### Run Tests

```bash
dotnet test PaycheckCalc.Tests
```

### Run the MAUI App

```bash
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

> **Note:** The MAUI app requires the `maui` workload and a supported target platform (Android emulator/device or Windows).

### Run the Blazor Server Web App

The Blazor head does **not** require the MAUI workload and can run on any supported OS:

```bash
dotnet run --project PaycheckCalc.Blazor
```

The web app loads tax JSON at startup from its build output (linked from `PaycheckCalc.Core/Data/`) and uses an interactive Server rendering mode with a per-circuit `CalculatorSessionState` shared between the inputs and results pages.

## How It Works

The **PayCalculator** orchestrates the full paycheck calculation pipeline:

1. **Gross Pay** — `(Regular Hours × Hourly Rate) + (Overtime Hours × Hourly Rate × OT Multiplier)`
2. **Deductions** — Pre-tax deductions reduce taxable wages; post-tax deductions are subtracted after taxes.
3. **FICA** — Social Security and Medicare are calculated on gross wages minus applicable pre-tax deductions, with annual wage-base caps.
4. **Federal Withholding** — Wages are annualized, the standard deduction and W-4 adjustments are applied, the tax is computed using graduated brackets from IRS Publication 15-T, and the result is de-annualized back to the pay period.
5. **State Withholding** — The `StateCalculatorRegistry` looks up the registered `IStateWithholdingCalculator` for the selected state and delegates calculation using the state's specific rules and inputs.
6. **Local Withholding** — When a local jurisdiction is selected, the `LocalCalculatorRegistry` delegates to a registered `ILocalWithholdingCalculator` (e.g., PA Act 32 EIT + LST, NYC, Ohio RITA/CCA, MD county surtax). Local taxes are additive — they reduce net pay but do not reduce federal or state taxable wages.
7. **Net Pay** — `Gross Pay − Pre-Tax Deductions − Post-Tax Deductions − Federal Tax − State Tax − State Disability − Social Security − Medicare − Additional Medicare − Local Withholding − Local Head Tax`

Gross pay, taxes, and deductions are rounded individually to two decimal places using `MidpointRounding.AwayFromZero` (round half away from zero). Net pay is computed from the unrounded components and then rounded so the displayed net equals `gross − taxes − deductions` to the cent.

## State Tax Coverage

All 50 states and the District of Columbia are supported. The architecture uses a plugin-based registry where each state implements `IStateWithholdingCalculator` and is registered centrally in `StateCalculatorRegistry` (see `MauiProgram.cs` / `PaycheckCalc.Blazor/Program.cs`). Every state has a dedicated calculator under `PaycheckCalc.Core/Tax/<State>/`; the shared `NoIncomeTaxWithholdingAdapter` is used only for the plain no-income-tax states, and the generic `PercentageMethodWithholdingAdapter` is retained for tests and potential future use but is not wired to any production state.

| Category | States |
|---|---|
| **No Income Tax** | AK, FL, NV, NH, SD, TN, TX (plus WA and WY, which have dedicated calculators — WA adds the WA Cares Fund 0.58% LTC premium with an opt-out toggle; WY has no state income tax and no employee-paid state payroll assessments) |
| **Flat Rate** | PA (3.07%) |
| **Dedicated state calculators with graduated / custom formulas** | AL, AR, AZ, CA (+ SDI), CO (+ FMLI), CT (+ PFMLI), DC, DE, GA, HI, IA, ID, IL, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, ND, NE, NJ, NM, NY, OH, OK, OR, RI, SC, UT, VA, VT, WI, WV |

Notable state-specific details:

- **AL** — graduated brackets with dependent deductions and annualized federal-withholding deduction.
- **AR** — Arkansas DFA formula method (JSON-backed tables).
- **CA** — Method B (EDD DE 44 percentage tables) plus State Disability Insurance.
- **CO** — flat 4.4% with DR 0004 Table 1 standard allowance and Family and Medical Leave Insurance premium.
- **CT** — TPG-211 table-driven withholding plus Paid Family & Medical Leave insurance.
- **DE** — DE W-4, 7 graduated brackets, $110 personal credit.
- **GA** — flat 5.19% per HB 111, G-4 filing statuses with allowance and dependent deductions.
- **IL** — flat 4.95% with IL-W-4 allowances.
- **OH** — IT-4 exemption ($650 annualized per exemption) with the two-bracket (0% up to $26,050, 2.75% over) Optional Computer Formula.
- **OK** — OW-2 percentage method (JSON-backed) with whole-dollar rounding.
- **OR** — OR-W-4 with a per-allowance tax *credit* (not deduction) and four graduated brackets.
- **UT** — flat 4.5% with phase-out allowance credit.

## Local (Sub-State) Tax Coverage

PaycheckCalc also models a growing set of local / sub-state payroll taxes via an `ILocalWithholdingCalculator` plugin model and `LocalCalculatorRegistry`. Current coverage:

| Jurisdiction | Calculator | Notes |
|---|---|---|
| Pennsylvania (Act 32) | `PaEitCalculator` | Earned Income Tax by municipality, JSON-backed rate table |
| Pennsylvania LST | `PaLstCalculator` | Flat per-pay-period Local Services Tax (head tax) |
| New York City | `NycWithholdingCalculator` | NYC resident personal income tax, JSON-backed |
| Ohio (RITA) | `OhioMunicipalCalculator` | Regional Income Tax Agency municipal rates, JSON-backed |
| Ohio (CCA) | `OhioMunicipalCalculator` | Central Collection Agency municipal rates, JSON-backed (same calculator handles both rate tables) |
| Maryland county surtax | `MdCountyCalculator` | County-level surtax paired with the MD state calculator, JSON-backed |

Local taxes are additive: they subtract from net pay but do **not** reduce federal or state taxable wages.

## UI Overview

The MAUI app groups its features under four flyout hubs — **Dashboard** (a landing page with quick-action tiles for New Paycheck, Saved Paychecks, Self-Employment, and Annual Tax Planner), **Paycheck Calculator**, **Self-Employment**, and **Annual Tax Planner**. Each hub (except Dashboard, which is a single page) opens to a top tab strip containing its sub-pages, so end-of-year 1040 features stay hidden until the user explicitly opens the Annual Tax Planner.

### Paycheck Calculator

- **Inputs** — A four-tab form for Pay & Hours, Federal W-4, State, and Deductions.
- **Results** — Two sub-tabs: **Period** (per-paycheck itemized taxes, deductions, net pay, doughnut chart, Save to Compare, Save as New Paycheck, and CSV/PDF export) and **Annual** (annualized projections, current paycheck number, and estimated year-end over/under withholding).
- **Compare** — Side-by-side comparison. Falls back to a 1-vs-1 saved-vs-current view, or renders a multi-scenario layout when scenarios are pushed from Saved Paychecks via `ComparisonSession`.
- **Saved** — Lists previously saved paycheck calculations with options to reload inputs into the calculator, rename, or delete entries, and push one or more into the multi-scenario comparison.

### Self-Employment

- **SE Inputs** — Three-tab input form (business income, expenses, QBI) for Schedule SE / Form 8995 calculations.
- **SE Results** — Two-tab results view showing self-employment tax breakdown and QBI deduction.

### Annual Tax Planner

A top tab strip whose tabs share a single `AnnualTaxSession` and persist saved annual scenarios via `JsonAnnualScenarioRepository`:

- **Jobs & YTD** — Multi-job income and year-to-date withholding inputs.
- **Adjustments** — Other Income & Adjustments to gross income (Schedule 1).
- **Credits** — Tax credits (CTC, EITC, education, etc.).
- **Estimates** — Quarterly estimated-tax payments.
- **What-If** — Scenario testing on top of the current projection.
- **Projection** — Annual projection summarizing the planner inputs.
- **Annual Results** — Year-end estimate combining withholding, credits, NIIT, and balance due / refund.

The Blazor Server head mirrors the MAUI feature surface at a smaller scale. Its `NavMenu` groups three sections — **Paycheck** (Inputs / Results / Saved Paychecks), **Self-Employment** (SE Inputs / SE Results), and **Home**. Per-circuit state lives in two scoped services, `CalculatorSessionState` and `SelfEmploymentSessionState`, and saved paychecks persist across browser sessions through `LocalStoragePaycheckRepository`, a `IPaycheckRepository` implementation backed by `localStorage` via a small JS-interop shim (`wwwroot/js/paycheckStorage.js`).

## Documentation

- [Wiki](docs/wiki/Home.md) — Full project wiki covering architecture, tax engine, state coverage, self-employment module, UI guide, and contributing guidelines.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the full architecture.
