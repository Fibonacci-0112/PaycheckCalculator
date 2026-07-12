---
applyTo: "PaycheckCalculator.Core/**/*.cs"
---

# Core library instructions

- `PaycheckCalculator.Core` must remain UI-agnostic. Do not add MAUI, XAML, Blazor, ASP.NET Core, or view-model dependencies here.
- Preserve sealed/init-only/value-oriented model patterns where they already exist.
- All money, wages, rates, thresholds, and deduction values must use `decimal`. Never introduce `double` or `float`.
- Keep `PayCalculator` as the orchestrator. It composes gross pay, pre-tax deductions, FICA, federal withholding, state withholding, and net pay. Do not push state-specific or supplemental logic into it.
- Additional calculators in `Pay/` each own their own pipeline: `BonusCalculator` (supplemental wages), `GrossUpCalculator` (bisection on gross to hit a target net), `SelfEmploymentCalculator` (SE tax + quarterly estimates), `AnnualProjectionCalculator`, and `HourlySalaryCalculator`. Keep each calculator focused on its own domain.
- Prefer extending `IStateWithholdingCalculator`, JSON-backed calculators, or `PercentageMethodConfig` over adding ad hoc conditionals inside existing calculators.
- State schemas are served by `JsonStateSchemaProvider` reading `Data/Schemas/*.json`. Keep schema files, C# field definitions, and UI field resolution in sync.
- Budgeting domain lives in `Budgeting/`: `BudgetCategory`, `BudgetSummary`, `AllocationRules`. Keep budget math here; DTOs and sync belong in Shared.
- Keep comments strong around legal or table-driven tax rules — readability matters because this code encodes legal rules.
- When editing a state calculator, inspect its matching tests and update them if behavior changes.
- If code and docs disagree, trust the implementation and tests first, then repair docs separately.
