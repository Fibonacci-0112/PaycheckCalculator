# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

PaycheckCalculator is a simple US paycheck calculator (2026 tax tables) with two front-ends — a **.NET MAUI** app (`PaycheckCalculator.App`, Android, iOS, Mac Catalyst, and Windows) and a **Blazor Server** web app (`PaycheckCalculator.Blazor`) — both backed by the same UI-agnostic core engine (`PaycheckCalculator.Core`) and exercised by an xUnit suite (`PaycheckCalculator.Tests`). It takes pay/W-4/state/deduction inputs and computes gross pay, federal/state withholding, FICA, deductions, and net pay. The annual projection (annualized totals, projected YTD, year-end over/under withholding) is shown in **both** front-ends, on a Per Paycheck / Annual sub-tab of the results. There is no annual Form 1040 planner. A **self-employment / 1099 module** (a calculation mode in both front-ends) estimates self-employment tax, state income tax, and quarterly estimated payments on annual net earnings; it does not model federal income tax.

Saved paychecks can optionally sync across the two front-ends via a user account. Two more projects support this: `PaycheckCalculator.Shared` (wire/storage contracts, JSON serialization, the last-write-wins merge, the typed HTTP client, and an `ISavedPaycheckStore` abstraction) and `PaycheckCalculator.Api` (a standalone ASP.NET Core Web API with ASP.NET Core Identity email/password accounts over EF Core PostgreSQL, with SQLite used by integration tests, exposing a `/api/paychecks/sync` endpoint). The MAUI app **also persists saved paychecks locally on device** (works fully offline / without an account); the Blazor app keeps anonymous saved paychecks **only until the browser tab closes** (circuit-scoped memory). See `docs/wiki/Accounts-and-Sync.md`.

Solution: `PaycheckCalculator.slnx`. The SDK version is pinned in `global.json` (11.0.100-preview.7.26381.103, latestFeature roll-forward, prerelease allowed). A project wiki lives in `docs/wiki/` and a Mermaid class diagram in `docs/class-diagram.md`.

## Common commands

All commands run from the repository root.

```bash
# Restore + build the whole solution (requires the MAUI workload)
dotnet build

# Build a single project (Core, Shared, Api, Blazor, and Tests build without the MAUI workload)
dotnet build PaycheckCalculator.Core

# Run all tests (transitively builds Core, Shared, Api, and Blazor)
dotnet test PaycheckCalculator.Tests

# Set up a machine from scratch: pinned SDK + MAUI workloads for this host + Android SDK
scripts/setup-dev.sh --all          # Linux/macOS   (Windows: pwsh scripts/setup-dev.ps1 -All)
source scripts/dotnet-env.sh        # every new shell

# Run the apps under test (needs a running app, not just a compiler)
dotnet test PaycheckCalculator.E2ETests           # Blazor in a real browser (Playwright)
dotnet test PaycheckCalculator.UITests.Android    # MAUI on a device/emulator (Appium)

# Run a single test class or single test
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"

# Run the Blazor web app (no MAUI workload needed)
dotnet run --project PaycheckCalculator.Blazor

# Run the sync API (no MAUI workload needed; defaults to http://localhost:5201)
dotnet run --project PaycheckCalculator.Api

# Build / run the MAUI app (requires `dotnet workload install maui` and a target platform)
dotnet build PaycheckCalculator.App
dotnet run --project PaycheckCalculator.App
```

`PaycheckCalculator.Core`, `PaycheckCalculator.Shared`, `PaycheckCalculator.Api`, `PaycheckCalculator.Blazor`, and `PaycheckCalculator.Tests` target `net11.0` and build on Linux without the MAUI workload. The Blazor app calls the API server-side (so no CORS); for end-to-end account/sync testing run `PaycheckCalculator.Api` and `PaycheckCalculator.Blazor` together.

`PaycheckCalculator.App` targets `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst`, and `net11.0-windows`; Apple targets are included only on macOS and the Windows target only on Windows. **Android does build on Linux** — install the `maui-android` workload (not `maui`, which needs Windows or macOS) plus a JDK and the Android SDK; `scripts/setup-dev.sh` does all of it. iOS/Mac Catalyst need macOS + Xcode and WinUI needs Windows, with no workaround.

### CI/CD and the test suites

| Workflow | Runners | Covers |
|---|---|---|
| `.github/workflows/ci.yml` | ubuntu | `PaycheckCalculator.Tests`; Blazor publish → **start** → browser end-to-end; API publish. |
| `.github/workflows/maui-build.yml` | ubuntu + windows + macos | MAUI compiles for all four target platforms. |
| `.github/workflows/maui-uitests.yml` | ubuntu + windows + macos | MAUI **launches and is driven** on emulator / simulator / desktop. |
| `.github/workflows/codeql.yml` | — | **Disabled by GitHub**: the repo uses CodeQL *default setup*, which forces any advanced workflow to `disabled_manually`. Its `build-mode: none` config is therefore inert; scanning still happens via default setup. The two are mutually exclusive. |
| `.github/workflows/release.yml` | ubuntu + windows + macos | On a `v*` tag: builds shippable artifacts, drafts a Release. Signing is optional. |

All of them share the composite action `.github/actions/setup-dotnet-maui`, which reads the SDK version from `global.json`, picks the workload set for the runner OS, and reuses `scripts/install-android-sdk.sh` — the same script local setup runs, so CI and dev cannot drift.

Two suites exist beyond the unit tests and are **not** part of `dotnet test PaycheckCalculator.Tests`, because they need a running app rather than a compiler:

- `PaycheckCalculator.E2ETests` — xUnit + Playwright, drives the Blazor app in a real browser.
- `PaycheckCalculator.UITests.{Android,iOS,MacCatalyst,Windows}` — NUnit + Appium, drives the MAUI app on a device. Shared test code lives in `PaycheckCalculator.UITests.Shared/` and is **linked** into each runner project (not project-referenced): NUnit binds a `[SetUpFixture]` to fixtures by namespace, so every runner assembly must compile them alongside its own `AppiumSetup`. All five share the namespace `PaycheckCalculator.UITests` — do not change it.

These are the only tests that catch a tax JSON renamed without updating the `MauiAsset` / `TaxData` linker entries: that mistake compiles cleanly and fails only at run time on device. If you add a control the tests need to reach, give it an `AutomationId` in XAML (MAUI) or a `data-testid` attribute (Blazor). See `docs/wiki/Development-Environment.md`.

## Architecture

### Layering (do not blur)

- `PaycheckCalculator.Core` is the calculation engine and **must stay free of MAUI / UI dependencies**. All money values use `decimal` — never `double`/`float`.
- `PaycheckCalculator.App` (MAUI) follows MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Pages are thin; the shared `CalculatorViewModel` owns state and commands; **mappers** translate between domain types and presentation models (`PaycheckInputMapper`, `ResultCardMapper` → `ResultCardModel`). Folder conventions: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`, `Services`. No tax math in code-behind, converters, or drawables.
- The MAUI shell is a four-tab `TabBar`: **Inputs** (`Views/InputsPage.xaml`, four sub-tabs — Pay & Hours, Federal, State, Deductions — each with a Calculate button), **Results** (`Views/ResultsPage.xaml`, a Per Paycheck / Annual sub-tab — the per-period summary with the doughnut chart, plus the web-style annual projection — with "Show Your Work" explanation popups, toolbar actions to Print (`Services/Printing`, native print dialog), Export PDF (`Services/Pdf`), and Export CSV (`Services/Csv`, opened in the default CSV app / Excel) — each export covers the per-period summary **and** the annual projection, appends the A/B comparison when two saved paychecks are selected, and is named via an editable file-name box (the renderers take the `ResultCardModel` plus an optional `AnnualProjectionModel`/`ComparisonRow`s and return bytes; the services just persist them under that name), and a first-run empty state), **Paychecks** (`Views/PaychecksPage.xaml`, the saved-paychecks list and A/B comparison, whose card has its own comparison-only PDF/CSV export), and **Account** (`Views/AccountPage.xaml`, optional sign-in / account creation / sync / server URL).
- `PaycheckCalculator.Blazor` is an interactive Blazor Server app (`Components/`): a single calculator page with inputs and results side by side, plus a full-width "Saved Paychecks & Account" panel below (which also offers an A/B side-by-side comparison of the session's saved paychecks via `Services/PaycheckComparison` → `Models/ComparisonRow`, mirroring the MAUI Paychecks page). Inputs mirror the MAUI app (including schema-driven state fields) and add YTD Social Security / Medicare wage fields; results split into **Per Paycheck** and **Annual** (the MAUI Results page mirrors this with a Per Paycheck / Annual sub-tab). The results panel also offers **Export CSV**, **Export PDF**, and **Print** (`Services/Export/` renders from the domain `PaycheckResult` plus an optional `AnnualProjection`/`ComparisonRow`s, mirroring the MAUI exporters' format — per-period **and** annual projection, with the A/B comparison appended when two saved paychecks are selected and an editable file-name box; the Compare A/B panel has its own comparison-only export; a small `wwwroot/export.js` handles the browser download and `window.print()`, with a `@media print` stylesheet in `app.css`).
- Both front-ends call `AddPaycheckCalculatorCore` (in `PaycheckCalculator.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`) — from `MauiProgram.cs` and the Blazor `Program.cs` respectively — supplying a platform `ITaxDataReader`: the MAUI app reads tax JSON from the app package (`MauiAppPackageTaxDataReader`), the Blazor app from a `TaxData/` folder in the build output (`FileSystemTaxDataReader`).
- Tax JSON tables live in `PaycheckCalculator.Core/Data/` and are content-linked into `PaycheckCalculator.Tests/` (`<None Include="..\PaycheckCalculator.Core\Data\…" Link="…">`), linked into the Blazor build output as `TaxData\*` in `PaycheckCalculator.Blazor.csproj`, and packaged into the MAUI app as `MauiAsset` items in `PaycheckCalculator.App.csproj`. **If you rename a JSON file, update every linker entry (Tests, Blazor, App), the loader in `AddPaycheckCalculatorCore`, and the tests that reference it.**
- **Sync layering (do not blur):** `PaycheckCalculator.Shared` (references Core only) owns the wire/storage contracts (`SavedPaycheckDto`/`SavedPaycheckResultDto`/`SavedPaycheckTombstone`/`SavedPaycheckSet`), the JSON config (`PaycheckJson` + `StateInputValuesJsonConverter`, which keeps the `StateInputValues` bag as real CLR primitives — never `JsonElement`), the deterministic last-write-wins merge (`SavedPaycheckMerger`, keyed by case-insensitive name), the typed `PaycheckApiClient` (with `ITokenStore`/`IApiBaseAddressProvider` abstractions), and the `ISavedPaycheckStore` + `PaycheckSyncService` orchestration. `PaycheckCalculator.Api` references Shared and must never reference App/Blazor; Core stays HTTP- and persistence-free. The store has three implementations: `JsonFilePaycheckStore` (MAUI, on-device JSON), `SessionPaycheckStore` (Blazor, circuit memory), and the server's EF Core rows. The merge logic is defined once in Shared and reused by the API and clients.

### Calculation pipeline

`PaycheckCalculator.Core/Pay/PayCalculator.cs` is the orchestrator. It composes — and must not absorb — the following per-paycheck steps in order:

1. Gross pay = `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)`.
2. Pre-tax deductions reduce taxable wages; post-tax deductions reduce net only.
3. FICA via `Tax/Fica/FicaCalculator.cs` (SS 6.2% capped at $184,500, Medicare 1.45%, Additional Medicare 0.9% > $200k).
4. Federal withholding via `Tax/Federal/Irs15TPercentageCalculator.cs` (IRS Pub 15-T 2026 percentage method, automated payroll systems): annualize → standard deduction + W-4 adjustments → graduated brackets → de-annualize.
5. State withholding: `StateCalculatorRegistry` looks up the state's `IStateWithholdingCalculator` and delegates.
6. Net pay is computed from unrounded components; gross/taxes/deductions round individually to two decimals using `MidpointRounding.AwayFromZero`, and net is rounded so it equals `gross − taxes − deductions` to the cent.

`Pay/AnnualProjectionCalculator.cs` extends a per-period result into the annual projection shown on both front-ends' Annual results tab (annualized totals, projected YTD by paycheck number, estimated year-end over/under withholding). The `Explanation/` types carry the "Show Your Work" step-by-step breakdowns attached to `PaycheckResult`.

`Pay/GrossUpCalculator.cs` is the inverse of the pipeline: given a target net pay it bisects on gross — treating `PayCalculator.Calculate` as a monotonic forward function and re-running it at every probe so brackets, FICA caps, and percentage deductions stay correct — and returns a `GrossUpResult` (required gross, the per-period `PaycheckResult` at that gross, and the cost). It is wired in `AddPaycheckCalculatorCore` and surfaced in **both** front-ends via a calculation-mode toggle.

`Pay/BonusCalculator.cs` is a separate (non-annualized) pipeline for supplemental wages: it composes `Tax/Federal/FederalSupplementalCalculator.cs` (IRS Pub 15 §7 flat-rate method — 22%, 37% on cumulative supplemental wages over $1,000,000), the same `FicaCalculator` (so the SS cap and Additional Medicare threshold are honored via YTD wages), and `Tax/Supplemental/StateSupplementalCalculator.cs` (data-driven from `Data/state_supplemental_2026.json` — per-state flat rate, Vermont's % of federal, no-tax, or a `RegularMethod` flag for states with no flat supplemental rate) into a `BonusResult` with a `BonusInput`. It is wired in `AddPaycheckCalculatorCore` and surfaced in **both** front-ends via the same calculation-mode toggle. It does **not** model state disability/paid-leave levies (e.g. CA SDI) on bonuses.

`Pay/SelfEmploymentCalculator.cs` is a separate (annual, non-projected) pipeline for self-employed / 1099 contractors. From annual net earnings (Schedule C net profit) it computes the federal **self-employment tax** — 15.3% (12.4% Social Security to the $184,500 wage base + 2.9% Medicare, i.e. both the employer and employee halves), plus 0.9% Additional Medicare over $200,000 — on 92.35% of earnings (sharing the `FicaCalculator` wage base / threshold so SE tax stays in sync). It estimates **state income tax** by running the full earnings through the ordinary `StateCalculatorRegistry` engine at an annual frequency — there is no separate *state* self-employment tax anywhere in the U.S.; states tax self-employment income as ordinary income, and state disability/leave levies are not applied. It also builds a **quarterly estimated-payment schedule** (Form 1040-ES due dates, federal + state split) into a `SelfEmploymentResult` with a `SelfEmploymentInput`. It does **not** model federal income tax (that depends on filing status / other income and is settled at filing). It is wired in `AddPaycheckCalculatorCore` and surfaced in **both** front-ends via the same calculation-mode toggle.

### Schema-driven state tax architecture

All 50 states plus DC are supported. Every state has a dedicated folder under `PaycheckCalculator.Core/Tax/<StateName>/` with its own `IStateWithholdingCalculator` implementation, registered centrally in `StateCalculatorRegistry` (wiring in `AddPaycheckCalculatorCore`). The state UI is **schema-driven**: each calculator returns `StateFieldDefinition`s from `GetInputSchema()` (backed by `JsonStateSchemaProvider` over `Data/Schemas/*.json`), both UIs bind those dynamically (MAUI via `StateFieldViewModel`), and inputs flow back as `StateInputValues`.

When adding/changing state inputs, keep all four in sync: schema, validation, UI field resolution, and tests. Do **not** hardcode per-state controls in the UI when the schema can express it. The shared `NoIncomeTaxWithholdingAdapter` is for plain no-tax states (AK/FL/NV/NH/SD/TN/TX); WA and WY have dedicated calculators (WA Cares Fund 0.58% with opt-out; WY empty schema). The generic `PercentageMethodWithholdingAdapter` is retained for tests and is **not** wired to any production state.

## Conventions

- **Money & rounding:** `decimal` everywhere. Don't introduce new rounding behavior or "simplify away" annualization, allowance handling, low-income exemptions, or per-period table logic — these encode legal rules. If a tax rule looks odd, check the matching test before changing it.
- **State-specific quirks that are intentional** (until replaced with a verified, tested fix): California Method B includes a deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`; Oklahoma OW-2 uses whole-dollar rounding; Alabama withholding depends on annualized federal withholding plus dependent deductions.
- **Tax data JSON:** authoritative structured data. Preserve key names and shapes unless the matching C# loader/model is being updated in the same change. Don't rename data files casually — `.csproj` linker entries, DI loaders, and tests depend on stable names. Keep edits traceable to the tax year and source publication.
- **Tests:** xUnit, scenario-based names matching the existing suite. Use **explicit numeric expected values** taken from the rule/table — do NOT recompute the expected value with production helpers. Cover bracket boundaries, exemption / allowance handling, extra withholding, pre-tax deduction effects, rounding edges, and state-specific exceptions. When you change a calculator, update its corresponding `*Test.cs` file.
- **Docs vs code:** if comments/docs disagree with the implementation and tests, **trust the implementation and tests** and repair docs as a separate concern.
- **Don't change** target frameworks, tax data file names, or tax-data asset wiring (MauiAsset items, Blazor `TaxData` links, test links) unless the task explicitly requires it. Prefer focused edits over broad refactors in tax code.
