# PaycheckCalc

A simple US paycheck calculator (2026 tax tables) that computes net pay, tax withholdings, and deductions for all 50 US states plus DC. It ships two front-ends — a **.NET MAUI** app (Android & Windows) and a **Blazor Server** web app — both backed by the same UI-agnostic core calculation library, `PaycheckCalc.Core`.

## Features

- **Gross Pay Calculation** — Computes gross pay from hourly rate, regular hours, and overtime hours with a configurable overtime multiplier.
- **Gross-Up Calculator** — Works the paycheck math backward: enter a desired net (take-home) amount and `GrossUpCalculator` solves for the gross pay that delivers it after federal, FICA, state, and deduction withholding — useful for net bonuses and relocation payments. Available in both apps via a calculation-mode toggle on the Pay tab.
- **Federal Income Tax** — Implements the IRS Publication 15-T (2026) percentage method for automated payroll systems, supporting all W-4 inputs (filing status, Step 2 checkbox, Step 3 credits, Step 4 adjustments).
- **FICA Taxes** — Calculates Social Security (6.2%, capped at $184,500), Medicare (1.45%), and Additional Medicare (0.9% above $200,000). Optional YTD Social Security / Medicare wage inputs (exposed in the web UI) handle the wage base cap and Additional Medicare threshold mid-year.
- **State Income Tax** — Covers all 50 states and DC. Every state ships its own `IStateWithholdingCalculator` implementation under `PaycheckCalc.Core/Tax/<State>/`, registered centrally via `StateCalculatorRegistry`:
  - **9 no-income-tax states** — AK, FL, NV, NH, SD, TN, TX, WA, WY. Most use the shared `NoIncomeTaxWithholdingAdapter`; WA has a dedicated calculator for the WA Cares Fund (0.58% LTC premium with opt-out), and WY has its own dedicated (empty-schema) calculator.
  - **Flat-rate state** — Pennsylvania (3.07%).
  - **All other states** — Each implements its own withholding rules (W-4 / state-specific certificate, standard deductions, allowances / exemptions, graduated brackets or flat rate, and state-specific credits). Examples include Alabama (graduated + dependents + federal deduction), Arkansas (DFA formula method), California (Method B, EDD DE 44 withholding tables + SDI), Colorado (flat 4.4% + DR 0004 Table 1 allowance + FMLI), Connecticut (TPG-211 table-driven withholding + PFMLI), Delaware (DE W-4, 7 graduated brackets + $110 personal credit), Georgia (flat 5.19% per HB 111 + G-4 allowances and dependent deductions), Illinois (flat 4.95% + IL-W-4 allowances), Ohio (IT-4 exemptions + two-bracket formula), Oklahoma (OW-2), Oregon (OR-W-4 with per-allowance tax credit), Utah (flat 4.5% with phase-out allowance credit), Virginia (VA-4), Wisconsin (WT-4), West Virginia (IT-104), and the remaining states that use an annualized graduated-bracket approach with state-specific deductions and allowances.
- **State Disability / Family Leave Insurance** — California SDI, Colorado FMLI (0.044%), Connecticut PFMLI, and the Washington WA Cares Fund (0.58% LTC premium) are computed alongside state withholding with dynamic labels on the results screen.
- **Pre-Tax & Post-Tax Deductions** — Supports configurable deductions as fixed dollar amounts or a percentage of gross, with per-deduction control over which wage bases (federal, state, FICA) a pre-tax deduction reduces.
- **Dynamic State Inputs** — Each state declares its own input schema (filing status, allowances, dependents, extra withholding), and the UI renders fields dynamically.
- **"Show Your Work" Explanations** — Every result line — gross pay, federal/FICA/state taxable income, each tax, state disability, and net pay — carries a step-by-step breakdown of how the number was computed, opened from the info icon next to the line.
- **Results Visualization** — Doughnut chart breakdown of gross pay (net pay, federal tax, Social Security, Medicare, state income tax, state disability insurance, and deductions) with percentage labels formatted to two decimal places.
- **PDF Export (MAUI app)** — Exports the per-period results to a single-page Letter-size PDF via a built-in minimal PDF writer (no external PDF packages) and opens it in the system PDF viewer.
- **Annual Projection (web app)** — `AnnualProjectionCalculator` in Core annualizes a paycheck, projects YTD totals by paycheck number, and estimates year-end over/under withholding; the Blazor app shows it on the Annual results tab. The MAUI app shows per-period results only.
- **Multiple Pay Frequencies** — Weekly, Bi-Weekly, Semi-Monthly, Monthly, Quarterly, Semi-Annual, Annual, and Daily.

## Project Structure

```
PaycheckCalc.slnx
├── PaycheckCalc.App/          # .NET MAUI frontend (Android & Windows)
│   ├── Views/                 # XAML pages (Inputs, Results)
│   ├── ViewModels/            # MVVM view models (Calculator, StateField, DeductionItem)
│   ├── Mappers/               # Domain-to-UI mappers (PaycheckInputMapper, ResultCardMapper)
│   ├── Models/                # UI presentation models (ResultCardModel)
│   ├── Controls/              # Custom controls (DoughnutChartDrawable)
│   ├── Behaviors/             # Input formatting behaviors
│   ├── Helpers/               # Enum display helpers and XAML value converters
│   ├── Services/              # MauiAppPackageTaxDataReader (tax JSON loading)
│   │   └── Pdf/               # Single-page PDF export: renderer, minimal PDF writer,
│   │                          #   and platform PDF viewer launcher
│   └── MauiProgram.cs         # DI configuration & app startup
├── PaycheckCalc.Blazor/       # Blazor Server web frontend
│   ├── Components/
│   │   ├── Pages/             # Calculator page (inputs and results side by side)
│   │   └── Shared/            # DoughnutChart (SVG), ExplanationModal
│   ├── Services/              # FileSystemTaxDataReader (tax JSON loading from TaxData/)
│   └── Program.cs             # DI configuration & app startup
├── PaycheckCalc.Core/         # Business logic (no UI dependencies)
│   ├── Models/                # PaycheckInput/Result, Enums, UsState, Deduction, AnnualProjection
│   ├── Pay/                   # PayCalculator (main orchestrator), AnnualProjectionCalculator
│   ├── Explanation/           # "Show Your Work" engine (PaycheckExplanation, LineExplanation,
│   │                          #   ExplanationStep, ExplanationLineKey) — step-by-step
│   │                          #   walkthroughs of every line on a paycheck
│   ├── DependencyInjection/   # AddPaycheckCalcCore + ITaxDataReader abstraction
│   ├── Data/                  # JSON tax tables (IRS 15-T, OK OW-2, CA Method B, AR,
│   │                          #   CO DR 0004, CT TPG-211)
│   │   └── Schemas/           # One JSON file per state (ak.json … wy.json) declaring that
│   │                          #   state's input schema for the schema-driven UI
│   └── Tax/
│       ├── Federal/           # IRS 15-T percentage calculator
│       ├── Fica/              # Social Security & Medicare calculator
│       ├── State/             # State tax interfaces, registry, schema provider, shared
│       │                      #   explanation steps, generic percentage-method adapter,
│       │                      #   and no-income-tax adapter
│       └── <State>/           # One folder per state (Alabama, Arizona, … Wyoming)
│                              #   each containing a dedicated IStateWithholdingCalculator
│                              #   implementation for that state's withholding rules
├── PaycheckCalc.Tests/        # xUnit test suite
└── docs/                      # Wiki and class diagrams
```

## Technology Stack

| Component | Technology |
|---|---|
| **Frameworks** | .NET 11 — MAUI (PaycheckCalc.App), ASP.NET Core Blazor Server (PaycheckCalc.Blazor) |
| **Target Platforms** | Android, Windows 10+, web browser |
| **UI Patterns** | MVVM with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MAUI); interactive server-rendered Razor components (Blazor) |
| **Test Framework** | xUnit 2.9.3 |
| **Tax Data** | JSON-based IRS 15-T and state tax bracket tables (2026) |

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) (preview) — the solution targets `net11.0` (see `global.json` for the pinned SDK version and roll-forward settings)
- .NET MAUI workload (only required for the MAUI App project; the Blazor, Core, and Tests projects build without it):
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

### Run the Web App

```bash
dotnet run --project PaycheckCalc.Blazor
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

**Gross-up** runs this pipeline in reverse. `GrossUpCalculator` takes a target net amount and binary-searches for the gross that produces it, re-running the full pipeline at every probe so graduated brackets, FICA wage-base caps, and percentage-of-gross deductions are all honored. It reports the required gross, the cost of taxes and deductions covered, and the complete per-period breakdown at the solved gross.

Every step also emits the `Explanation/` records that power the "Show Your Work" breakdowns in both front-ends.

Both front-ends wire the engine up through `AddPaycheckCalcCore` (in `PaycheckCalc.Core/DependencyInjection/`), supplying an `ITaxDataReader` for their platform: the MAUI app reads the tax JSON from the app package (`MauiAppPackageTaxDataReader`), and the Blazor app reads it from a `TaxData/` folder in the build output (`FileSystemTaxDataReader`).

## State Tax Coverage

All 50 states and the District of Columbia are supported. The architecture uses a plugin-based registry where each state implements `IStateWithholdingCalculator` and is registered centrally in `StateCalculatorRegistry` (see `AddPaycheckCalcCore` in `PaycheckCoreServiceCollectionExtensions.cs`, called from both `MauiProgram.cs` and the Blazor `Program.cs`). Every state has a dedicated calculator under `PaycheckCalc.Core/Tax/<State>/`; the shared `NoIncomeTaxWithholdingAdapter` is used only for the plain no-income-tax states, and the generic `PercentageMethodWithholdingAdapter` is retained for tests and potential future use but is not wired to any production state.

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

### MAUI app

A two-tab Shell:

- **Inputs** — Four sub-tabs: Pay & Hours, Federal (W-4), State, and Deductions. The Pay & Hours sub-tab has a **Mode** selector that switches between a standard paycheck and a gross-up (enter a desired net pay to find the required gross). The State sub-tab renders each state's input fields dynamically from its schema and surfaces state-specific validation errors. Every sub-tab has a Calculate button.
- **Results** — A single per-period summary: an income card (gross pay plus federal, FICA, and state taxable income), tax withholdings, deductions, net pay, and the doughnut chart, with tap-for-explanation info icons on each line. An **Export PDF** toolbar button saves the summary as a single-page PDF and opens it in the system PDF viewer. Before the first calculation the page shows a friendly empty state.

### Blazor web app

A single calculator page with the inputs panel and results panel side by side. Inputs mirror the MAUI app (including dynamic schema-driven state fields and the standard/gross-up calculation-mode selector) and add YTD Social Security / Medicare wage fields. Results are split into **Per Paycheck** (itemized taxes, deductions, net pay, an SVG doughnut chart, and "Show Your Work" explanation modals) and **Annual** (annualized totals, projected YTD by paycheck number, and estimated year-end over/under withholding). In gross-up mode the results lead with a summary card (desired net, taxes & deductions covered, required gross) above the full breakdown.

## Documentation

- [Wiki](docs/wiki/Home.md) — Full project wiki covering architecture, tax engine, state coverage, UI guide, and contributing guidelines.
- [UML Class Diagram](docs/class-diagram.md) — Mermaid-based class diagram of the architecture.
