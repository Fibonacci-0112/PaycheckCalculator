# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

PaycheckCalc is a simple US paycheck calculator (2026 tax tables) with two front-ends — a **.NET MAUI** app (`PaycheckCalc.App`, Android & Windows) and a **Blazor Server** web app (`PaycheckCalc.Blazor`) — both backed by the same UI-agnostic core engine (`PaycheckCalc.Core`) and exercised by an xUnit suite (`PaycheckCalc.Tests`). It takes pay/W-4/state/deduction inputs and computes gross pay, federal/state withholding, FICA, deductions, and net pay. The annual projection (annualized totals, projected YTD, year-end over/under withholding) is shown **only in the Blazor app**; the MAUI app shows per-period results only. There is no annual Form 1040 planner or self-employment module.

Solution: `PaycheckCalc.slnx`. The SDK version is pinned in `global.json` (10.0.x, latestPatch roll-forward). A project wiki lives in `docs/wiki/` and a Mermaid class diagram in `docs/class-diagram.md`.

## Common commands

All commands run from the repository root.

```bash
# Restore + build the whole solution (requires the MAUI workload)
dotnet build

# Build a single project (Core, Blazor, and Tests build without the MAUI workload)
dotnet build PaycheckCalc.Core

# Run all tests
dotnet test PaycheckCalc.Tests

# Run a single test class or single test
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"

# Run the Blazor web app (no MAUI workload needed)
dotnet run --project PaycheckCalc.Blazor

# Build / run the MAUI app (requires `dotnet workload install maui` and a target platform)
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

`PaycheckCalc.Core` multi-targets `net10.0;net9.0` when the .NET 10 SDK is present, otherwise it falls back to `net9.0` only. `PaycheckCalc.App` targets `net10.0-android` / `net10.0-windows`; `PaycheckCalc.Blazor` and `PaycheckCalc.Tests` are `net10.0` only. CI (`.github/workflows/dotnet.yml`) builds and tests `PaycheckCalc.Tests` on Linux; the MAUI and Blazor apps are not built in CI (`codeql.yml` runs CodeQL scanning separately).

## Architecture

### Layering (do not blur)

- `PaycheckCalc.Core` is the calculation engine and **must stay free of MAUI / UI dependencies**. All money values use `decimal` — never `double`/`float`.
- `PaycheckCalc.App` (MAUI) follows MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Pages are thin; the shared `CalculatorViewModel` owns state and commands; **mappers** translate between domain types and presentation models (`PaycheckInputMapper`, `ResultCardMapper` → `ResultCardModel`). Folder conventions: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`, `Services`. No tax math in code-behind, converters, or drawables.
- The MAUI shell is a two-tab `TabBar`: **Inputs** (`Views/InputsPage.xaml`, four sub-tabs — Pay & Hours, Federal, State, Deductions — each with a Calculate button) and **Results** (`Views/ResultsPage.xaml`, a single per-period summary with the doughnut chart, "Show Your Work" explanation popups, an Export PDF toolbar button backed by `Services/Pdf`, and a first-run empty state).
- `PaycheckCalc.Blazor` is an interactive Blazor Server app (`Components/`): a single calculator page with inputs and results side by side. Inputs mirror the MAUI app (including schema-driven state fields) and add YTD Social Security / Medicare wage fields; results split into **Per Paycheck** and **Annual** (the annual projection is web-only).
- Both front-ends call `AddPaycheckCalcCore` (in `PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`) — from `MauiProgram.cs` and the Blazor `Program.cs` respectively — supplying a platform `ITaxDataReader`: the MAUI app reads tax JSON from the app package (`MauiAppPackageTaxDataReader`), the Blazor app from a `TaxData/` folder in the build output (`FileSystemTaxDataReader`).
- Tax JSON tables live in `PaycheckCalc.Core/Data/` and are content-linked into `PaycheckCalc.Tests/` (`<None Include="..\PaycheckCalc.Core\Data\…" Link="…">`), linked into the Blazor build output as `TaxData\*` in `PaycheckCalc.Blazor.csproj`, and packaged into the MAUI app as `MauiAsset` items in `PaycheckCalc.App.csproj`. **If you rename a JSON file, update every linker entry (Tests, Blazor, App), the loader in `AddPaycheckCalcCore`, and the tests that reference it.**

### Calculation pipeline

`PaycheckCalc.Core/Pay/PayCalculator.cs` is the orchestrator. It composes — and must not absorb — the following per-paycheck steps in order:

1. Gross pay = `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)`.
2. Pre-tax deductions reduce taxable wages; post-tax deductions reduce net only.
3. FICA via `Tax/Fica/FicaCalculator.cs` (SS 6.2% capped at $184,500, Medicare 1.45%, Additional Medicare 0.9% > $200k).
4. Federal withholding via `Tax/Federal/Irs15TPercentageCalculator.cs` (IRS Pub 15-T 2026 percentage method, automated payroll systems): annualize → standard deduction + W-4 adjustments → graduated brackets → de-annualize.
5. State withholding: `StateCalculatorRegistry` looks up the state's `IStateWithholdingCalculator` and delegates.
6. Net pay is computed from unrounded components; gross/taxes/deductions round individually to two decimals using `MidpointRounding.AwayFromZero`, and net is rounded so it equals `gross − taxes − deductions` to the cent.

`Pay/AnnualProjectionCalculator.cs` extends a per-period result into the annual projection shown on the Blazor app's Annual results tab (annualized totals, projected YTD by paycheck number, estimated year-end over/under withholding). The `Explanation/` types carry the "Show Your Work" step-by-step breakdowns attached to `PaycheckResult`.

### Schema-driven state tax architecture

All 50 states plus DC are supported. Every state has a dedicated folder under `PaycheckCalc.Core/Tax/<StateName>/` with its own `IStateWithholdingCalculator` implementation, registered centrally in `StateCalculatorRegistry` (wiring in `AddPaycheckCalcCore`). The state UI is **schema-driven**: each calculator returns `StateFieldDefinition`s from `GetInputSchema()` (backed by `JsonStateSchemaProvider` over `Data/Schemas/*.json`), both UIs bind those dynamically (MAUI via `StateFieldViewModel`), and inputs flow back as `StateInputValues`.

When adding/changing state inputs, keep all four in sync: schema, validation, UI field resolution, and tests. Do **not** hardcode per-state controls in the UI when the schema can express it. The shared `NoIncomeTaxWithholdingAdapter` is for plain no-tax states (AK/FL/NV/NH/SD/TN/TX); WA and WY have dedicated calculators (WA Cares Fund 0.58% with opt-out; WY empty schema). The generic `PercentageMethodWithholdingAdapter` is retained for tests and is **not** wired to any production state.

## Conventions

- **Money & rounding:** `decimal` everywhere. Don't introduce new rounding behavior or "simplify away" annualization, allowance handling, low-income exemptions, or per-period table logic — these encode legal rules. If a tax rule looks odd, check the matching test before changing it.
- **State-specific quirks that are intentional** (until replaced with a verified, tested fix): California Method B includes a deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`; Oklahoma OW-2 uses whole-dollar rounding; Alabama withholding depends on annualized federal withholding plus dependent deductions.
- **Tax data JSON:** authoritative structured data. Preserve key names and shapes unless the matching C# loader/model is being updated in the same change. Don't rename data files casually — `.csproj` linker entries, DI loaders, and tests depend on stable names. Keep edits traceable to the tax year and source publication.
- **Tests:** xUnit, scenario-based names matching the existing suite. Use **explicit numeric expected values** taken from the rule/table — do NOT recompute the expected value with production helpers. Cover bracket boundaries, exemption / allowance handling, extra withholding, pre-tax deduction effects, rounding edges, and state-specific exceptions. When you change a calculator, update its corresponding `*Test.cs` file.
- **Docs vs code:** if comments/docs disagree with the implementation and tests, **trust the implementation and tests** and repair docs as a separate concern.
- **Don't change** target frameworks, tax data file names, or tax-data asset wiring (MauiAsset items, Blazor `TaxData` links, test links) unless the task explicitly requires it. Prefer focused edits over broad refactors in tax code.
