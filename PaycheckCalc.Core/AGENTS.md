# AGENTS.md - PaycheckCalc.Core

Scope: this file applies to everything under `PaycheckCalc.Core/`.

## Role of this project

`PaycheckCalc.Core` is the UI-agnostic calculation engine for paycheck, tax, gross-up, annual projection, budgeting, recurring bill, savings goal, and report logic. Keep it usable from MAUI, Blazor, the API, and tests without taking dependencies on any of those outer layers.

## Hard boundaries

- Do not reference MAUI, Blazor, ASP.NET Core hosting, EF Core persistence, HTTP clients, platform APIs, or UI presentation models from Core.
- Money and rates that affect money must use `decimal`; do not introduce `double` or `float` into calculation paths.
- Preserve the paycheck pipeline shape: gross pay, deductions, FICA, federal withholding, state withholding, paid-leave/disability premiums, then net pay.
- Keep state withholding schema-driven through `StateFieldDefinition`, `StateInputValues`, schema JSON, and `IStateWithholdingCalculator.GetInputSchema()`.
- Do not hardcode front-end behavior here. Core should expose data/results/explanations, not UI choices.

## Tax and data rules

- Treat `Data/*.json` and `Data/Schemas/*.json` as structured source data. Preserve existing key names and shapes unless loader/model/test changes are part of the same edit.
- If a tax data file is renamed, update all consumers in the same change: Core loaders, MAUI `MauiAsset` entries, Blazor `TaxData` links, Tests linked files, and tests that reference the file name.
- When adding or changing a state calculator, update its folder under `Tax/<StateName>/`, register it centrally, add/update its schema, and add scenario tests.
- Keep odd-looking verified quirks unless replacing them with source-backed tests. Examples include California's current Method B cent adjustment, Oklahoma whole-dollar rounding, and Alabama's dependence on annualized federal withholding.

## Testing expectations

- Add or update xUnit tests in `PaycheckCalc.Tests` for every calculation change.
- Use explicit expected numeric values from the legal rule/table or hand calculation. Do not compute test expectations by calling production helpers.
- Cover bracket boundaries, exemption thresholds, allowances, extra withholding, pre-tax deduction effects, rounding edges, YTD wage caps, and state-specific exceptions.

## Useful commands

```bash
dotnet build PaycheckCalc.Core
dotnet test PaycheckCalc.Tests --filter "FullyQualifiedName~<CalculatorOrScenarioName>"
```

`PaycheckCalc.Core` multi-targets based on the installed SDK. Avoid APIs that would break the `net9.0` fallback unless the project file is intentionally changed.
