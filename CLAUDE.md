# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

PaycheckCalc is a simple US paycheck calculator (2026 tax tables) with two front-ends — a **.NET MAUI** app (`PaycheckCalc.App`, Android & Windows) and a **Blazor Server** web app (`PaycheckCalc.Blazor`) — both backed by the same UI-agnostic core engine (`PaycheckCalc.Core`) and exercised by an xUnit suite (`PaycheckCalc.Tests`). It takes pay/W-4/state/deduction inputs and computes gross pay, federal/state withholding, FICA, deductions, and net pay. The annual projection (annualized totals, projected YTD, year-end over/under withholding) is shown in **both** front-ends, on a Per Paycheck / Annual sub-tab of the results. There is no annual Form 1040 planner or self-employment module.

Saved paychecks can optionally sync across the two front-ends via a user account. Two more projects support this: `PaycheckCalc.Shared` (wire/storage contracts, JSON serialization, the last-write-wins merge, the typed HTTP client, and an `ISavedPaycheckStore` abstraction) and `PaycheckCalc.Api` (a standalone ASP.NET Core Web API with ASP.NET Core Identity email/password accounts over EF Core SQLite, exposing a `/api/paychecks/sync` endpoint). The MAUI app **also persists saved paychecks locally on device** (works fully offline / without an account); the Blazor app keeps anonymous saved paychecks **only until the browser tab closes** (circuit-scoped memory). See `docs/wiki/Accounts-and-Sync.md`.

Solution: `PaycheckCalc.slnx`. The SDK version is pinned in `global.json` (11.0.100-preview.5.26302.115, latestPatch roll-forward, prerelease allowed). A project wiki lives in `docs/wiki/` and a Mermaid class diagram in `docs/class-diagram.md`.

## Common commands

All commands run from the repository root.

```bash
# Restore + build the whole solution (requires the MAUI workload)
dotnet build

# Build a single project (Core, Shared, Api, Blazor, and Tests build without the MAUI workload)
dotnet build PaycheckCalc.Core

# Run all tests (transitively builds Shared + Api, which the suite references)
dotnet test PaycheckCalc.Tests

# Run a single test class or single test
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"

# Run the Blazor web app (no MAUI workload needed)
dotnet run --project PaycheckCalc.Blazor

# Run the sync API (no MAUI workload needed; defaults to http://localhost:5201)
dotnet run --project PaycheckCalc.Api

# Build / run the MAUI app (requires `dotnet workload install maui` and a target platform)
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

`PaycheckCalc.Shared`, `PaycheckCalc.Api`, `PaycheckCalc.Blazor`, and `PaycheckCalc.Tests` are `net11.0` and build on Linux/CI without the MAUI workload. The Blazor app calls the API server-side (so no CORS); for end-to-end account/sync testing run `PaycheckCalc.Api` and `PaycheckCalc.Blazor` together.

`PaycheckCalc.Core` multi-targets `net11.0;net9.0` when the .NET 11 SDK is present, otherwise it falls back to `net9.0` only. `PaycheckCalc.App` targets `net11.0-android` / `net11.0-windows`; `PaycheckCalc.Blazor` and `PaycheckCalc.Tests` are `net11.0` only. CI (`.github/workflows/dotnet.yml`) builds and tests `PaycheckCalc.Tests` on Linux; the MAUI and Blazor apps are not built in CI (`codeql.yml` runs CodeQL scanning separately).

## Architecture

### Layering (do not blur)

- `PaycheckCalc.Core` is the calculation engine and **must stay free of MAUI / UI dependencies**. All money values use `decimal` — never `double`/`float`.
- `PaycheckCalc.App` (MAUI) follows MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Pages are thin; the shared `CalculatorViewModel` owns state and commands; **mappers** translate between domain types and presentation models (`PaycheckInputMapper`, `ResultCardMapper` → `ResultCardModel`). Folder conventions: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`, `Services`. No tax math in code-behind, converters, or drawables.
- The MAUI shell is a four-tab `TabBar`: **Inputs** (`Views/InputsPage.xaml`, four sub-tabs — Pay & Hours, Federal, State, Deductions — each with a Calculate button), **Results** (`Views/ResultsPage.xaml`, a Per Paycheck / Annual sub-tab — the per-period summary with the doughnut chart, plus the web-style annual projection — with "Show Your Work" explanation popups, toolbar actions to Print (`Services/Printing`, native print dialog), Export PDF (`Services/Pdf`), and Export CSV (`Services/Csv`, opened in the default CSV app / Excel) — each export covers the per-period summary **and** the annual projection, appends the A/B comparison when two saved paychecks are selected, and is named via an editable file-name box (the renderers take the `ResultCardModel` plus an optional `AnnualProjectionModel`/`ComparisonRow`s and return bytes; the services just persist them under that name), and a first-run empty state), **Paychecks** (`Views/PaychecksPage.xaml`, the saved-paychecks list and A/B comparison, whose card has its own comparison-only PDF/CSV export), and **Account** (`Views/AccountPage.xaml`, optional sign-in / account creation / sync / server URL).
- `PaycheckCalc.Blazor` is an interactive Blazor Server app (`Components/`): a single calculator page with inputs and results side by side, plus a full-width "Saved Paychecks & Account" panel below (which also offers an A/B side-by-side comparison of the session's saved paychecks via `Services/PaycheckComparison` → `Models/ComparisonRow`, mirroring the MAUI Paychecks page). Inputs mirror the MAUI app (including schema-driven state fields) and add YTD Social Security / Medicare wage fields; results split into **Per Paycheck** and **Annual** (the MAUI Results page mirrors this with a Per Paycheck / Annual sub-tab). The results panel also offers **Export CSV**, **Export PDF**, and **Print** (`Services/Export/` renders from the domain `PaycheckResult` plus an optional `AnnualProjection`/`ComparisonRow`s, mirroring the MAUI exporters' format — per-period **and** annual projection, with the A/B comparison appended when two saved paychecks are selected and an editable file-name box; the Compare A/B panel has its own comparison-only export; a small `wwwroot/export.js` handles the browser download and `window.print()`, with a `@media print` stylesheet in `app.css`).
- Both front-ends call `AddPaycheckCalcCore` (in `PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`) — from `MauiProgram.cs` and the Blazor `Program.cs` respectively — supplying a platform `ITaxDataReader`: the MAUI app reads tax JSON from the app package (`MauiAppPackageTaxDataReader`), the Blazor app from a `TaxData/` folder in the build output (`FileSystemTaxDataReader`).
- Tax JSON tables live in `PaycheckCalc.Core/Data/` and are content-linked into `PaycheckCalc.Tests/` (`<None Include="..\PaycheckCalc.Core\Data\…" Link="…">`), linked into the Blazor build output as `TaxData\*` in `PaycheckCalc.Blazor.csproj`, and packaged into the MAUI app as `MauiAsset` items in `PaycheckCalc.App.csproj`. **If you rename a JSON file, update every linker entry (Tests, Blazor, App), the loader in `AddPaycheckCalcCore`, and the tests that reference it.**
- **Sync layering (do not blur):** `PaycheckCalc.Shared` (references Core only) owns the wire/storage contracts (`SavedPaycheckDto`/`SavedPaycheckResultDto`/`SavedPaycheckTombstone`/`SavedPaycheckSet`), the JSON config (`PaycheckJson` + `StateInputValuesJsonConverter`, which keeps the `StateInputValues` bag as real CLR primitives — never `JsonElement`), the deterministic last-write-wins merge (`SavedPaycheckMerger`, keyed by case-insensitive name), the typed `PaycheckApiClient` (with `ITokenStore`/`IApiBaseAddressProvider` abstractions), and the `ISavedPaycheckStore` + `PaycheckSyncService` orchestration. `PaycheckCalc.Api` references Shared and must never reference App/Blazor; Core stays HTTP- and persistence-free. The store has three implementations: `JsonFilePaycheckStore` (MAUI, on-device JSON), `SessionPaycheckStore` (Blazor, circuit memory), and the server's EF Core rows. The merge logic is defined once in Shared and reused by the API and clients.

### Calculation pipeline

`PaycheckCalc.Core/Pay/PayCalculator.cs` is the orchestrator. It composes — and must not absorb — the following per-paycheck steps in order:

1. Gross pay = `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)`.
2. Pre-tax deductions reduce taxable wages; post-tax deductions reduce net only.
3. FICA via `Tax/Fica/FicaCalculator.cs` (SS 6.2% capped at $184,500, Medicare 1.45%, Additional Medicare 0.9% > $200k).
4. Federal withholding via `Tax/Federal/Irs15TPercentageCalculator.cs` (IRS Pub 15-T 2026 percentage method, automated payroll systems): annualize → standard deduction + W-4 adjustments → graduated brackets → de-annualize.
5. State withholding: `StateCalculatorRegistry` looks up the state's `IStateWithholdingCalculator` and delegates.
6. Net pay is computed from unrounded components; gross/taxes/deductions round individually to two decimals using `MidpointRounding.AwayFromZero`, and net is rounded so it equals `gross − taxes − deductions` to the cent.

`Pay/AnnualProjectionCalculator.cs` extends a per-period result into the annual projection shown on both front-ends' Annual results tab (annualized totals, projected YTD by paycheck number, estimated year-end over/under withholding). The `Explanation/` types carry the "Show Your Work" step-by-step breakdowns attached to `PaycheckResult`.

`Pay/GrossUpCalculator.cs` is the inverse of the pipeline: given a target net pay it bisects on gross — treating `PayCalculator.Calculate` as a monotonic forward function and re-running it at every probe so brackets, FICA caps, and percentage deductions stay correct — and returns a `GrossUpResult` (required gross, the per-period `PaycheckResult` at that gross, and the cost). It is wired in `AddPaycheckCalcCore` and surfaced in **both** front-ends via a calculation-mode toggle.

`Pay/BonusCalculator.cs` is a separate (non-annualized) pipeline for supplemental wages: it composes `Tax/Federal/FederalSupplementalCalculator.cs` (IRS Pub 15 §7 flat-rate method — 22%, 37% on cumulative supplemental wages over $1,000,000), the same `FicaCalculator` (so the SS cap and Additional Medicare threshold are honored via YTD wages), and `Tax/Supplemental/StateSupplementalCalculator.cs` (data-driven from `Data/state_supplemental_2026.json` — per-state flat rate, Vermont's % of federal, no-tax, or a `RegularMethod` flag for states with no flat supplemental rate) into a `BonusResult` with a `BonusInput`. It is wired in `AddPaycheckCalcCore` and surfaced in **both** front-ends via the same calculation-mode toggle. It does **not** model state disability/paid-leave levies (e.g. CA SDI) on bonuses.

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
