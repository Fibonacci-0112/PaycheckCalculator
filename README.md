# PaycheckCalc

A simple **.NET MAUI** paycheck calculator (Android & Windows) that computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. The UI is two tabs — enter your pay details on **Inputs**, see the breakdown on **Results** — backed by a UI-agnostic core calculation library.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000).
- **State Income Tax** — Covers all 50 states and DC. Every state ships its own `IStateWithholdingCalculator` implementation under `PaycheckCalc.Core/Tax/<State>/`, registered centrally via `StateCalculatorRegistry`:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY. Most use the shared `NoIncomeTaxWithholdingAdapter`; WA has a dedicated calculator for the WA Cares Fund (0.58% LTC premium with opt-out), and WY has its own dedicated (empty-schema) calculator.
  - **Flat-rate state** — Pennsylvania (3.07%).
  - **All other states** — Each implements its own withholding rules (W-4 / state-specific certificate, standard deductions, allowances / exemptions, graduated brackets or flat rate, and state-specific credits). Examples include Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables + SDI), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Connecticut (TPG-211 table-driven withholding + PFMLI), Delaware (DE W-4, 7 graduated brackets + $110 personal credit), Georgia (flat 5.19% per HB 111 + G-4 allowances and dependent deductions), Illinois (flat 4.95% + IL-W-4 allowances), Ohio (IT-4 exemptions + two-bracket formula), Oklahoma (OW-2), Oregon (OR-W-4 with per-allowance tax credit), Utah (flat 4.5% with phase-out allowance credit), Virginia (VA-4), Wisconsin (WT-4), West Virginia (IT-104), and the remaining states that use an annualized graduated-bracket approach with state-specific deductions and allowances.
- **State Disability / Family Leave Insurance** — California SDI, Colorado FMLI (0.044%), Connecticut PFMLI, and the Washington WA Cares Fund (0.58% LTC premium) are computed alongside state withholding with dynamic labels on the results screen.
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions that reduce taxable wages.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **Annual Projection** — Estimates annualized gross, taxes, and net pay; tracks current paycheck number and remaining pay periods, and projects year-end over/under withholding.
- **"Show Your Work" Explanations** — Tap the info icon next to any result line for a step-by-step breakdown of how that number was computed.
- **Results Visualization** — Doughnut chart breakdown of gross pay by category (federal tax, state tax, Social Security, Medicare, net pay).
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Inputs, Results)
│   ├── ViewModels/            # MVVM view models (Calculator, StateField, DeductionItem)
│   ├── Mappers/               # Domain-to-UI mappers (PaycheckInput, ResultCard, AnnualProjection)
│   ├── Models/                # UI presentation models (ResultCardModel, AnnualProjectionModel)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers and XAML value converters
│   ├── Services/              # MauiAppPackageTaxDataReader (tax JSON loading)
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput/Result, Enums, Deduction, AnnualProjection
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator
│   ├── Explanation/           # "Show Your Work" engine (PaycheckExplanation, LineExplanation,
│   │                          #   ExplanationStep, ExplanationLineKey) — step-by-step
│   │                          #   walkthroughs of every line on a paycheck
│   ├── Data/                  # JSON tax tables (IRS 15-T, OK OW-2, CA Method B, AR,
│   │                          #   CO DR 0004, CT TPG-211)
│   │   └── Schemas/           # One JSON file per state (al.json … wy.json) declaring that
│   │                          #   state's input schema for the schema-driven UI
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── State/             # State tax interfaces, registry, generic percentage-method adapter,
│       │                      #   and no-income-tax adapter
│       └── <State>/           # One folder per state (Alabama, Arizona, … Wyoming)
│                              #   each containing a dedicated IStateWithholdingCalculator
│                              #   implementation for that state's withholding rules
├── PaycheckCalc.Tests/        # xUnit test suite
└── docs/                      # Class diagrams and documentation
```

## Technology Stack

| Component | Technology |
|---|---|
| **Framework** | .NET 10 — MAUI (PaycheckCalc.App) |
| **Target Platforms** | Android, Windows 10+ |
| **UI Pattern** | MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| **Test Framework** | xUnit 2.9.3 |
| **Tax Data** | JSON-based IRS 15-T and state tax bracket tables (2026) |

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

## How It Works

The **PayCalculator** orchestrates the full paycheck calculation pipeline:

1. **Gross Pay** — `(Regular Hours × Hourly Rate) + (Overtime Hours × Hourly Rate × OT Multiplier)`
2. **Deductions** — Pre-tax deductions reduce taxable wages; post-tax deductions are subtracted after taxes.
3. **FICA** — Social Security and Medicare are calculated on gross wages minus applicable pre-tax deductions, with annual wage-base caps.
4. **Federal Withholding** — Wages are annualized, the standard deduction and W-4 adjustments are applied, the tax is computed using graduated brackets from IRS Publication 15-T, and the result is de-annualized back to the pay period.
5. **State Withholding** — The `StateCalculatorRegistry` looks up the registered `IStateWithholdingCalculator` for the selected state and delegates calculation using the state's specific rules and inputs.
6. **Net Pay** — `Gross Pay − Pre-Tax Deductions − Post-Tax Deductions − Federal Tax − State Tax − State Disability − Social Security − Medicare − Additional Medicare`

Gross pay, taxes, and deductions are rounded individually to two decimal places using `MidpointRounding.AwayFromZero` (round half away from zero). Net pay is computed from the unrounded components and then rounded so the displayed net equals `gross − taxes − deductions` to the cent.

## State Tax Coverage

All 50 states and the District of Columbia are supported. The architecture uses a plugin-based registry where each state implements `IStateWithholdingCalculator` and is registered centrally in `StateCalculatorRegistry` (see `MauiProgram.cs`). Every state has a dedicated calculator under `PaycheckCalc.Core/Tax/<State>/`; the shared `NoIncomeTaxWithholdingAdapter` is used only for the plain no-income-tax states, and the generic `PercentageMethodWithholdingAdapter` is retained for tests and potential future use but is not wired to any production state.

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

## UI Overview

The app is a single two-tab Shell:

- **Inputs** — A four-section form for Pay & Hours, Federal W-4, State, and Deductions. The State section renders each state's input fields dynamically from its schema. Every section has a Calculate button.
- **Results** — Two sub-tabs: **Period** (per-paycheck itemized taxes, deductions, net pay, and a doughnut chart, with tap-for-explanation info icons on each line) and **Annual** (annualized projections, current paycheck number, and estimated year-end over/under withholding).

## Documentation

- [Wiki](docs/wiki/Home.md) — Full project wiki covering architecture, tax engine, state coverage, UI guide, and contributing guidelines.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the architecture.
