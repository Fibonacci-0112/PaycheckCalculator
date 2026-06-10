# Contributing

This page covers the development workflow, testing expectations, and coding conventions for PaycheckCalc contributors.

---

## Development Workflow

1. **Clone the repository** and ensure the .NET 10 SDK (pinned in `global.json`) is installed.
2. **Build the Core library** to verify your environment:
   ```bash
   dotnet build PaycheckCalc.Core
   ```
3. **Run the test suite** before making changes to establish a baseline:
   ```bash
   dotnet test PaycheckCalc.Tests
   ```
4. **Make focused changes** — prefer small, surgical edits over broad refactors, especially in tax code.
5. **Run tests again** to confirm nothing is broken.
6. **Submit a pull request** with a clear description of what changed and why.

---

## Project Conventions

### Money and Decimal Usage

- Use `decimal` for **all** monetary values, wages, taxes, rates, thresholds, and deduction values.
- Never use `double` or `float` in calculation code.
- All final monetary results are rounded to 2 decimal places using `MidpointRounding.AwayFromZero`.
- Preserve existing rounding behavior unless you have a tax-law-backed reason to change it, along with corresponding test updates.

### Code Organization

- **Core stays UI-free.** Do not add MAUI, XAML, or view-model dependencies to `PaycheckCalc.Core`.
- **PayCalculator is an orchestrator.** It composes calculation steps but does not contain state-specific tax rules.
- **State calculators are self-contained plugins.** Tax rules for a specific state belong in `PaycheckCalc.Core/Tax/<StateName>/`.
- **Mappers translate, not compute.** The mapper layer converts between domain and UI models without performing business logic.
- **Pages are thin.** View models own state and commands. Code-behind should only contain view-specific behavior.

### Naming and Structure

- Follow the existing folder structure: `Views`, `ViewModels`, `Models`, `Mappers`, `Helpers`, `Controls`, `Behaviors`.
- Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) consistently.
- Use existing helper types (`PickerItem<T>`, `EnumDisplay`, `StateFieldViewModel`, `DecimalFormatBehavior`) rather than duplicating patterns.

### Tax Logic

- Prefer small, explicit calculation steps with comments when implementing tax rules. Readability matters because this code encodes legal/business rules.
- Do not "simplify" away state-specific branches, annualization rules, allowance handling, low-income exemptions, or per-period table logic.
- Keep JSON-backed tax logic data-driven where the repository already uses JSON tables.
- If a tax rule appears odd, inspect the tests before changing it — it may be intentional.
- Keep comments strong around legal or table-driven tax rules.

---

## Testing Expectations

### General Rules

- Any non-trivial tax logic change **must** be accompanied by test updates or new tests.
- Prefer **explicit expected dollar amounts** taken from the applicable tax publication, rather than recomputing expected values with production helpers.
- Keep tests readable and scenario-based with descriptive names.

### What to Cover

- Filing statuses and their effects on brackets/deductions.
- Bracket boundaries (testing values just below, at, and above thresholds).
- Allowance and exemption handling.
- Extra withholding inputs.
- Pre-tax deduction effects on taxable wages.
- Rounding behavior at boundaries.
- State-specific quirks (e.g., Alabama's federal deduction, California's 3-cent adjustment, Oklahoma's whole-dollar rounding).

### Test File Conventions

- Each calculator has a matching test file: `<CalculatorName>Test.cs` in `PaycheckCalc.Tests/`.
- Use xUnit with `[Fact]` and `[Theory]` attributes.
- Test names should describe the payroll scenario being verified.

---

## Adding a New State

See the [State Tax Coverage](State-Tax-Coverage.md#adding-a-new-state) page for step-by-step instructions on adding a state via the generic percentage method or a custom calculator.

Key checklist:
- [ ] Implement `IStateWithholdingCalculator` (or add a `StateTaxConfigs2026` entry).
- [ ] Define the input schema, validation, and calculation logic.
- [ ] Register the calculator in `MauiProgram.cs`.
- [ ] Add JSON data files if needed (and register as app package assets).
- [ ] Write regression tests with explicit expected values.
- [ ] Verify the state appears in the UI picker and dynamic fields render correctly.

---

## Things to Avoid

- **Do not hardcode per-state fields in XAML** when the schema-driven approach can express the requirement.
- **Do not add business logic to code-behind, converters, or drawables.**
- **Do not reload static tax JSON on every calculation.** Startup-time DI loading is the established pattern.
- **Do not change target frameworks, preview package versions, tax data file names, or MAUI asset wiring** unless the task explicitly calls for it.
- **Do not remove or edit unrelated tests.** This could mask bugs or missing functionality.
- **Do not introduce `double` or `float`** into any calculation path.

---

## Intentional Quirks

Some implementation details may look unusual but are intentional until replaced with verified fixes:

- **California** uses Method B, includes SDI, and has a deliberate 3-cent single-status adjustment in `CaliforniaWithholdingCalculator`.
- **Oklahoma** uses OW-2 JSON tables and whole-dollar rounding behavior.
- **Alabama** withholding depends on annualized federal withholding and dependent deductions.
- Some documentation or comments may lag behind the code — when they disagree, trust the implementation and tests first.
