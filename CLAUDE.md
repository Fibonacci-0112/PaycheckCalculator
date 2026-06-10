# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

PaycheckCalc is a cross-platform US paycheck calculator (2026 tax tables) with two front ends — a **.NET MAUI** app (`PaycheckCalc.App`, Android & Windows) and a **Blazor Server** web head (`PaycheckCalc.Blazor`) — both backed by a UI-agnostic core engine (`PaycheckCalc.Core`) and exercised by an xUnit suite (`PaycheckCalc.Tests`). It also implements Schedule SE / QBI and full annual Form 1040 estimation.

Solution: `PaycheckCalc.slnx`. SDK is pinned in `global.json` to `10.0.100` (stable, latestFeature roll-forward).

## Common commands

All commands run from the repository root.

```bash
# Restore + build the whole solution
dotnet build

# Build a single project
dotnet build PaycheckCalc.Core
dotnet build PaycheckCalc.Blazor

# Run all tests
dotnet test PaycheckCalc.Tests

# Run a single test class or single test
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"

# Run the Blazor Server web head (no MAUI workload required)
dotnet run --project PaycheckCalc.Blazor

# Build / run the MAUI app (requires `dotnet workload install maui` and a target platform)
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

`PaycheckCalc.Core` multi-targets `net10.0;net9.0` when the .NET 10 SDK is present, otherwise it falls back to `net9.0` only. The `net9.0` build excludes `Export/PdfPaycheckExporter.cs` and `Export/PdfSelfEmploymentExporter.cs` (QuestPDF is `net10.0`-only here). `PaycheckCalc.App`, `PaycheckCalc.Blazor`, and `PaycheckCalc.Tests` are `net10.0` only.

## Architecture

### Layering (do not blur)

- `PaycheckCalc.Core` is the calculation engine and **must stay free of MAUI / Blazor / UI dependencies**. All money values use `decimal` — never `double`/`float`.
- `PaycheckCalc.App` (MAUI) follows MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Pages are thin; view models own state and commands; **mappers** translate between domain types and presentation models (`ResultCardModel`, `AnnualProjectionModel`, `ScenarioSnapshot`, etc.). No tax math in code-behind, converters, or drawables.
- `PaycheckCalc.Blazor` is a Blazor Web App with interactive Server rendering. Per-circuit state lives in scoped services `CalculatorSessionState` and `SelfEmploymentSessionState`; saved paychecks persist via `LocalStoragePaycheckRepository` (browser `localStorage` through the `wwwroot/js/paycheckStorage.js` JS-interop shim).
- Tax JSON tables live in `PaycheckCalc.Core/Data/` and are content-linked into `PaycheckCalc.Tests/` and `PaycheckCalc.Blazor/wwwroot/data/` via `<Content Include="..\PaycheckCalc.Core\Data\…" Link="…">` in the consuming `.csproj`. **If you rename a JSON file, update every linker entry, the loader in `MauiProgram.cs` / `Program.cs`, and the tests that reference it.**

### Calculation pipeline

`PaycheckCalc.Core/Pay/PayCalculator.cs` is the orchestrator. It composes — and must not absorb — the following per-paycheck steps in order:

1. Gross pay = `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)`.
2. Pre-tax deductions reduce taxable wages; post-tax deductions reduce net only.
3. FICA via `Tax/Fica/FicaCalculator.cs` (SS 6.2% capped at $184,500, Medicare 1.45%, Additional Medicare 0.9% > $200k).
4. Federal withholding via `Tax/Federal/Irs15TPercentageCalculator.cs` (IRS Pub 15-T 2026 percentage method, automated payroll systems): annualize → standard deduction + W-4 adjustments → graduated brackets → de-annualize.
5. State withholding: `StateCalculatorRegistry` looks up the state's `IStateWithholdingCalculator` and delegates.
6. Local withholding: `LocalCalculatorRegistry` delegates to an `ILocalWithholdingCalculator` (PA EIT/LST, NYC, OH RITA/CCA, MD county surtax). **Local taxes are additive — they reduce net pay but do NOT reduce federal or state taxable wages.**
7. Net pay is computed from unrounded components; gross/taxes/deductions round individually to two decimals using `MidpointRounding.AwayFromZero`, and net is rounded so it equals `gross − taxes − deductions` to the cent.

`AnnualProjectionCalculator` builds the year-end annualization. Annual Form 1040 estimation lives in `Tax/Federal/Annual/` (`Form1040Calculator` orchestrates `Federal1040TaxCalculator`, `Schedule1Calculator`, `ChildTaxCreditCalculator`, `Form8863EducationCreditsCalculator`, `Form8880SaversCreditCalculator`, `Form8960NiitCalculator`, `Form1040ESCalculator`, `WithholdingSuggestionCalculator`). Self-employment is in `Tax/SelfEmployment/` (Schedule SE + Form 8995/8995-A QBI), and it coordinates with FICA so W-2 wages reduce remaining SS wage base / Additional Medicare threshold.

### Schema-driven state tax architecture

Every state has a dedicated folder under `PaycheckCalc.Core/Tax/<StateName>/` with its own `IStateWithholdingCalculator` implementation, registered centrally in `StateCalculatorRegistry` (wiring: `MauiProgram.cs` and `PaycheckCalc.Blazor/Program.cs`). The state UI is **schema-driven**: each calculator returns `StateFieldDefinition`s from `GetInputSchema()`, the UI binds those to `StateFieldViewModel` (MAUI) / dynamic field rendering (Blazor), and inputs flow back as `StateInputValues`.

When adding/changing state inputs, keep all four in sync: schema, validation, UI field resolution, and tests. Do **not** hardcode per-state controls in `InputsPage.xaml` or Razor pages when the schema can express it. The shared `NoIncomeTaxWithholdingAdapter` is for plain no-tax states (AK/FL/NV/NH/SD/TN/TX); WA and WY have dedicated calculators (WA Cares Fund 0.58% with opt-out; WY empty schema). The generic `PercentageMethodWithholdingAdapter` is retained for tests and is **not** wired to any production state.

### Local tax plugin model

Same pattern at the local level: `ILocalWithholdingCalculator` + `LocalCalculatorRegistry` under `Tax/Local/{Maryland,NewYork,Ohio,Pennsylvania}/`. Most are JSON-backed (rate tables in `Core/Data/`). PA also has a flat `PaLstCalculator` head tax.

### Front-end specifics

- **MAUI (`PaycheckCalc.App`)** organizes features into three flyout hubs (Paycheck Calculator, Self-Employment, Annual Tax Planner). Annual Tax Planner shares a single `AnnualTaxSession` across its tabs and persists scenarios via `JsonAnnualScenarioRepository`. Comparison is driven by `ComparisonSession`. Saved paychecks use `JsonPaycheckRepository` (local JSON files).
- **Blazor (`PaycheckCalc.Blazor`)** mirrors a smaller surface (Paycheck: Inputs/Results/Saved; Self-Employment: SE Inputs/Results; Home). Per-circuit scoped services hold state across pages.

## Conventions

- **Money & rounding:** `decimal` everywhere. Don't introduce new rounding behavior or "simplify away" annualization, allowance handling, low-income exemptions, or per-period table logic — these encode legal rules. If a tax rule looks odd, check the matching test before changing it.
- **State-specific quirks that are intentional** (until replaced with a verified, tested fix): California Method B includes a deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`; Oklahoma OW-2 uses whole-dollar rounding; Alabama withholding depends on annualized federal withholding plus dependent deductions.
- **Tax data JSON:** authoritative structured data. Preserve key names and shapes unless the matching C# loader/model is being updated in the same change. Don't rename data files casually — `.csproj` linker entries, DI loaders, and tests depend on stable names. Keep edits traceable to the tax year and source publication.
- **Tests:** xUnit, scenario-based names matching the existing suite. Use **explicit numeric expected values** taken from the rule/table — do NOT recompute the expected value with production helpers. Cover bracket boundaries, exemption / allowance handling, extra withholding, pre-tax deduction effects, rounding edges, and state-specific exceptions. When you change a calculator, update its corresponding `*Test.cs` file.
- **Docs vs code:** if comments/docs disagree with the implementation and tests, **trust the implementation and tests** and repair docs as a separate concern.
- **Don't change** target frameworks, preview package versions, tax data file names, or MAUI asset wiring unless the task explicitly requires it. Prefer focused edits over broad refactors in tax code.

## Branch policy for Claude sessions

All work in this session must be developed, committed, and pushed to the branch `claude/add-claude-documentation-hKh7u`. Create it locally if it does not exist. Push to the main branch and you are allowed to create pull requests without asking first.