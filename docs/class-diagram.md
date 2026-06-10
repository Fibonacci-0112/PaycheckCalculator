# UML Class Diagram

> High-level Mermaid class diagram for the **PaycheckCalc** solution.
> Render with any Mermaid-compatible viewer (GitHub markdown, VS Code extension, etc.).
>
> The diagram below is intentionally architectural rather than exhaustive: each
> per-state withholding calculator (50 states + DC) and each per-locality
> calculator implements the registry-driven interfaces shown here, so they are
> elided in favor of the contracts and registries that wire them together.

## Package overview

```mermaid
classDiagram
    direction TB

    class Core["PaycheckCalc.Core"] {
        <<library>>
        UI-agnostic tax engine
    }
    class App["PaycheckCalc.App (MAUI)"] {
        <<head>>
        Android & Windows MVVM
    }
    class Tests["PaycheckCalc.Tests"] {
        <<xUnit>>
    }

    App ..> Core : ProjectReference
    Tests ..> Core : ProjectReference
```

## Core — paycheck pipeline

```mermaid
classDiagram
    direction TB

    %% ── Domain models ───────────────────────────────────────
    class PaycheckInput {
        <<sealed>>
        +PayFrequency Frequency
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +UsState State
        +StateInputValues? StateInputValues
        +string? HomeLocalityCode
        +string? WorkLocalityCode
        +LocalInputValues? LocalInputValues
        +FederalW4Input FederalW4
        +IReadOnlyList~Deduction~ Deductions
        +decimal YtdSocialSecurityWages
        +decimal YtdMedicareWages
        +int PaycheckNumber
    }

    class PaycheckResult {
        <<sealed>>
        +decimal GrossPay
        +decimal PreTaxDeductions
        +decimal PostTaxDeductions
        +decimal FederalTaxableIncome
        +decimal FederalWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +UsState State
        +decimal StateTaxableWages
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
        +decimal LocalTaxableWages
        +decimal LocalWithholding
        +decimal LocalHeadTax
        +IReadOnlyList~LocalWithholdingLine~ LocalBreakdown
        +decimal TotalTaxes
        +decimal NetPay
        +PaycheckExplanation Explanation
    }

    class Deduction {
        +string Name
        +decimal Amount
        +DeductionType Type
        +DeductionAmountType AmountType
        +bool ReducesFederalTaxableWages
        +bool ReducesStateTaxableWages
        +bool ReducesFicaWages
    }

    class AnnualProjection {
        <<sealed>>
        +int PayPeriodsPerYear
        +int CurrentPaycheckNumber
        +int RemainingPaychecks
        +decimal AnnualizedGrossPay
        +decimal AnnualizedNetPay
        +decimal AnnualizedTotalWithholding
        +decimal EstimatedAnnualFederalLiability
        +decimal EstimatedAnnualFicaLiability
        +decimal OverUnderWithholding
    }

    %% ── Calculators ─────────────────────────────────────────
    class PayCalculator {
        +Calculate(PaycheckInput) PaycheckResult
    }

    class FicaCalculator {
        +Calculate(...) FicaResult
    }

    class Irs15TPercentageCalculator {
        +Calculate(...) decimal
    }

    class AnnualProjectionCalculator {
        +Calculate(PaycheckInput, PaycheckResult) AnnualProjection
    }

    %% ── State plugin model ──────────────────────────────────
    class IStateWithholdingCalculator {
        <<interface>>
        +UsState State
        +GetInputSchema() IReadOnlyList~StateFieldDefinition~
        +Validate(StateInputValues) IEnumerable~string~
        +Calculate(CommonWithholdingContext, StateInputValues) StateWithholdingResult
    }

    class StateCalculatorRegistry {
        +Register(IStateWithholdingCalculator)
        +IsSupported(UsState) bool
        +GetCalculator(UsState) IStateWithholdingCalculator
        +SupportedStates IReadOnlyList~UsState~
    }

    class IStateSchemaProvider {
        <<interface>>
        +GetSchema(UsState) IReadOnlyList~StateFieldDefinition~
    }

    class JsonStateSchemaProvider {
        loads Data/Schemas/*.json
    }

    %% ── Local plugin model ──────────────────────────────────
    class ILocalWithholdingCalculator {
        <<interface>>
        +LocalityId Locality
        +Calculate(CommonLocalWithholdingContext, LocalInputValues) LocalWithholdingResult
    }

    class LocalCalculatorRegistry {
        +Register(ILocalWithholdingCalculator)
        +GetCalculator(string code) ILocalWithholdingCalculator
    }

    %% ── Explanations ────────────────────────────────────────
    class PaycheckExplanation {
        +Get(ExplanationLineKey) LineExplanation?
    }

    class LineExplanation {
        +string Title
        +decimal FinalAmount
        +IReadOnlyList~ExplanationStep~ Steps
        +string? Reference
    }

    %% ── Relationships ───────────────────────────────────────
    PayCalculator --> StateCalculatorRegistry
    PayCalculator --> LocalCalculatorRegistry
    PayCalculator --> FicaCalculator
    PayCalculator --> Irs15TPercentageCalculator
    PayCalculator ..> PaycheckInput
    PayCalculator ..> PaycheckResult
    AnnualProjectionCalculator --> Irs15TPercentageCalculator
    AnnualProjectionCalculator --> FicaCalculator
    AnnualProjectionCalculator ..> AnnualProjection
    StateCalculatorRegistry o-- IStateWithholdingCalculator : 51 registered
    LocalCalculatorRegistry o-- ILocalWithholdingCalculator : PA EIT/LST, NYC, OH, MD
    IStateWithholdingCalculator ..> IStateSchemaProvider : schema lookup
    JsonStateSchemaProvider ..|> IStateSchemaProvider
    PaycheckInput o-- Deduction
    PaycheckResult o-- PaycheckExplanation
    PaycheckExplanation o-- LineExplanation
```

## MAUI head — MVVM layer

```mermaid
classDiagram
    direction TB

    class InputsPage {
        <<ContentPage>>
        four-section input form
    }

    class ResultsPage {
        <<ContentPage>>
        Period + Annual tabs, doughnut chart
    }

    class CalculatorViewModel {
        +Frequency / HourlyRate / Hours / OT
        +FederalW4 step properties
        +SelectedState + StateFields
        +Deductions ObservableCollection
        +ResultCard ResultCardModel?
        +Projection AnnualProjectionModel?
        +CalculateCommand
        +ShowExplanationCommand
    }

    class StateFieldViewModel {
        wraps one StateFieldDefinition
    }

    class DeductionItemViewModel {
        wraps one Deduction entry
    }

    class PaycheckInputMapper {
        +Map(vm, stateValues) PaycheckInput
    }

    class ResultCardMapper {
        +Map(PaycheckResult) ResultCardModel
    }

    class AnnualProjectionMapper {
        +Map(AnnualProjection) AnnualProjectionModel
    }

    class ResultCardModel {
        presentation-ready paycheck card
    }

    class AnnualProjectionModel {
        presentation-ready projection card
    }

    class DoughnutChartDrawable {
        +ResultCardModel? Result
        +Draw(canvas, rect)
    }

    InputsPage --> CalculatorViewModel : BindingContext
    ResultsPage --> CalculatorViewModel : BindingContext
    ResultsPage --> DoughnutChartDrawable
    CalculatorViewModel o-- StateFieldViewModel
    CalculatorViewModel o-- DeductionItemViewModel
    CalculatorViewModel ..> PaycheckInputMapper
    CalculatorViewModel ..> ResultCardMapper
    CalculatorViewModel ..> AnnualProjectionMapper
    ResultCardMapper ..> ResultCardModel
    AnnualProjectionMapper ..> AnnualProjectionModel
    DoughnutChartDrawable ..> ResultCardModel
```
