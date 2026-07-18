---
applyTo: "PaycheckCalculator.App/**/*.cs,PaycheckCalculator.App/**/*.xaml"
---

# MAUI app instructions

- Follow MVVM. Pages should stay thin; business logic belongs in `PaycheckCalculator.Core` or mappers, not in XAML code-behind.
- Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) for observable properties and commands.
- Preserve the mapper boundary: `CalculatorViewModel` builds `StateInputValues`, maps to `PaycheckInput`, and maps domain results to `ResultCardModel`.
- Keep the state section schema-driven. Do not hardcode per-state controls in XAML if a schema-driven field can express the requirement.
- Use existing helper types such as `PickerItem<T>`, `EnumDisplay`, `StateFieldViewModel`, and `DecimalFormatBehavior` instead of duplicating patterns.
- Keep UI naming and folder structure consistent: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`, `Services`.
- Services live under `Services/` and are organized by concern: `Csv` and `Pdf` (export renderers taking `ResultCardModel` + optional `AnnualProjectionModel`/`ComparisonRow`s and returning bytes), `Printing` (native print dialog), `Storage` (on-device JSON paycheck store `JsonFilePaycheckStore`), `Sync` (paycheck and budget sync orchestration via Shared services).
- Do not add calculation math to converters, drawables, or page code-behind.
- Do not reload static tax JSON on every calculation; startup-time DI loading is the pattern.
- State registration belongs in `MauiProgram` via `AddPaycheckCalculatorCore`/`StateCalculatorRegistry`, not in individual pages or view models.
- The shell is a four-tab `TabBar`: Inputs (Pay & Hours / Federal / State / Deductions), Results (Per Paycheck / Annual with doughnut chart and Show Your Work), Paychecks (saved list + A/B comparison with export), Account (sign-in / account creation / sync / server URL).
- If a UI change depends on new state inputs, update the state calculator schema and field resolution flow, not just the visual layer.
