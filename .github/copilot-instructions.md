# PaycheckCalculator repository instructions for GitHub Copilot

PaycheckCalculator is a US paycheck calculator (2026 tax tables) with two front-ends — a **.NET MAUI** app and a **Blazor Server** web app — both backed by a shared calculation engine and exercised by an xUnit suite. It computes gross pay, federal/state withholding, FICA, deductions, and net pay, plus annual projection, bonus withholding, gross-up, self-employment tax, and budgeting.

## Solution shape

| Project | Role |
|---|---|
| `PaycheckCalculator.Core` | Calculation engine; must stay free of any UI dependency |
| `PaycheckCalculator.App` | .NET MAUI front-end (Android + iOS + Mac Catalyst + Windows) |
| `PaycheckCalculator.Blazor` | Blazor Server front-end |
| `PaycheckCalculator.Shared` | Cross-client sync contracts, JSON config, merge logic, API client, and budgeting DTOs |
| `PaycheckCalculator.Api` | ASP.NET Core Web API providing account + sync endpoints (PostgreSQL via EF Core) |
| `PaycheckCalculator.Tests` | xUnit regression suite; part of the development workflow, not an afterthought |

Tax JSON tables live in `PaycheckCalculator.Core/Data/` and are asset-linked into the App (`MauiAsset`), copied into Blazor build output (`TaxData/`), and content-linked into Tests. If you rename a file, update every linker entry, the DI loader, and every test that references it.

## Layering rules — do not blur

- `PaycheckCalculator.Core` is the single source of tax and payroll math. No tax logic belongs in XAML, code-behind, Razor components, converters, exporters, or API endpoints.
- `PaycheckCalculator.Shared` may reference Core but must not reference MAUI, Blazor, ASP.NET Core hosting, EF Core, or platform storage APIs.
- `PaycheckCalculator.Api` references Shared and must not reference App or Blazor. It syncs stored results; it does not recalculate.
- Both front-ends call `AddPaycheckCalculatorCore` supplying a platform `ITaxDataReader` (MAUI: `MauiAppPackageTaxDataReader`; Blazor: `FileSystemTaxDataReader`).
- `StateCalculatorRegistry` is the single registration point for all state calculators and is wired in `AddPaycheckCalculatorCore`. Do not register states in front-end startup code.

## Calculation pipeline (PayCalculator)

`PayCalculator` is the orchestrator — compose, do not absorb:

1. Gross pay = `(RegularHours × Rate) + (OvertimeHours × Rate × OtMultiplier)`.
2. Pre-tax deductions reduce taxable wages; post-tax deductions reduce net only.
3. FICA via `FicaCalculator` (SS 6.2% to $184,500 cap; Medicare 1.45%; Additional Medicare 0.9% above $200k).
4. Federal withholding via `Irs15TPercentageCalculator` (IRS Pub 15-T 2026 percentage method): annualize → standard deduction + W-4 adjustments → graduated brackets → de-annualize.
5. State withholding via `StateCalculatorRegistry` → `IStateWithholdingCalculator`.
6. Net = gross − all taxes − deductions (rounded so components balance to the cent).

Additional calculators in `Pay/`:
- `AnnualProjectionCalculator` — annualizes a per-period result into projected YTD and year-end over/under estimates.
- `BonusCalculator` — supplemental wages (IRS flat-rate method, state supplemental rates from `state_supplemental_2026.json`).
- `GrossUpCalculator` — bisects on gross to hit a target net pay, re-running the full pipeline at every probe.
- `SelfEmploymentCalculator` — SE tax (15.3% on 92.35% of earnings + 0.9% Additional Medicare above $200k), state income tax estimate, and quarterly estimated payments.
- `HourlySalaryCalculator` — converts between hourly rates and annual/periodic equivalents.

## Money and tax logic rules

- Use `decimal` for **all** money, wages, rates, thresholds, and deduction values. Never introduce `double` or `float` into calculation code.
- Preserve existing rounding behavior unless the change is explicitly backed by a tax-law citation and corresponding test updates.
- Prefer small, explicit calculation steps with comments. This code encodes legal rules; readability matters.
- Do not "simplify away" annualization, allowance handling, low-income exemptions, or per-period table logic.
- If a tax rule looks odd, inspect the matching tests before changing it — it is probably intentional.

## State-tax architecture

All 50 states plus DC have dedicated modules under `PaycheckCalculator.Core/Tax/<StateName>/`, each implementing `IStateWithholdingCalculator`. The UI is schema-driven: calculators return `StateFieldDefinition`s from `GetInputSchema()` (backed by `JsonStateSchemaProvider` over `Data/Schemas/*.json`); both UIs bind fields dynamically from this schema. When adding or changing state inputs, keep schema, validation, field resolution, and tests aligned.

- `NoIncomeTaxWithholdingAdapter` handles AK, FL, NV, NH, SD, TN, TX.
- WA and WY have dedicated calculators (WA Cares Fund; WY empty schema).
- Do not hardcode per-state controls in the UI when the schema can express the requirement.

**Intentional state quirks** (do not silently fix without verified tax-law backing and test coverage):
- California: Method B, SDI, deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`.
- Oklahoma: OW-2 JSON tables with whole-dollar rounding.
- Alabama: withholding depends on annualized federal withholding and dependent deductions.

## Sync and budgeting

Saved paychecks and budgets can sync across front-ends via an optional account:
- `PaycheckCalculator.Shared` owns all DTOs (`SavedPaycheckDto`, `BudgetDto`, etc.), JSON converters, deterministic last-write-wins merge (`SavedPaycheckMerger`, `BudgetMerger`), the typed `PaycheckApiClient`, and the `ISavedPaycheckStore`/`IBudgetStore` abstractions.
- `PaycheckCalculator.Api` provides `/api/account` (ASP.NET Core Identity), `/api/paychecks`, and `/api/budgets` endpoints backed by EF Core + PostgreSQL. Tests use SQLite via `EnsureCreated`.
- Local persistence: MAUI uses `JsonFilePaycheckStore`/`JsonFileBudgetStore`; Blazor uses circuit-scoped `SessionPaycheckStore`/`SessionBudgetStore`.
- Merge logic lives once in Shared and is reused by the API and both clients. Do not duplicate or move it.
- `StateInputValues` must round-trip as real CLR primitives; `StateInputValuesJsonConverter` enforces this — never allow `JsonElement` to leak into consumers.

## Budgeting (Core)

`PaycheckCalculator.Core/Budgeting/` provides the budgeting domain: `BudgetCategory`, `BudgetSummary`, `AllocationRules` (50/30/20, zero-based starter), and transaction tracking. Keep budget math in Core; keep DTOs and sync in Shared.

## MAUI rules

- Follow MVVM with CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`).
- Keep pages thin. `CalculatorViewModel` owns state and commands; mappers translate between domain types and `ResultCardModel`.
- Shell is a four-tab `TabBar`: Inputs (Pay & Hours / Federal / State / Deductions), Results (Per Paycheck / Annual), Paychecks (saved list + A/B comparison), Account (sign-in / sync).
- Services live under `Services/`: `Csv`, `Pdf`, `Printing`, `Storage`, `Sync`.
- Do not reload static tax JSON on every calculation; startup-time DI loading is the pattern.

## Blazor rules

- Keep Razor components focused on rendering and binding. Tax, budget, projection, and merge logic belong in Core/Shared services.
- State input fields must be schema-driven; do not hardcode per-state controls.
- Anonymous saved-paycheck and budget state is circuit-scoped (`SessionPaycheckStore`, `SessionBudgetStore`).
- `wwwroot/export.js` is browser download/print glue only; no business logic.
- Exports live in `Services/Export/` and render from domain results; they do not recalculate.

## Testing rules

- Any non-trivial tax logic change must include updated or new tests.
- Use explicit numeric expected values taken from the applicable tax table or rule — do not recompute expected values with production helpers.
- Cover bracket boundaries, exemption/allowance handling, extra withholding, pre-tax deduction effects, rounding edges, state-specific exceptions, JSON round-trips, merge edge cases, and endpoint auth.
- Keep test names scenario-based and descriptive; they are living documentation.

## Change safety

- Do not change target frameworks, preview package versions, tax data file names, or asset wiring unless the task explicitly calls for it.
- If you touch JSON schemas, asset file names, or DTO field names, update all loaders, project linkers, and tests that depend on them.
- Prefer focused edits over broad refactors in tax code.
- If docs and implementation disagree, trust the implementation and tests; repair docs separately.
- Before any "cleanup," verify the current shape is not preserving tax accuracy, dynamic UI behavior, or test compatibility.
