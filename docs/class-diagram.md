# UML Class Diagram

> High-level Mermaid class diagram for the **PaycheckCalc** solution.
> Render with any Mermaid-compatible viewer (GitHub markdown, VS Code extension, etc.).
>
> The diagram below is intentionally architectural rather than exhaustive: each
> per-state withholding calculator (50 states + DC) implements the
> registry-driven interfaces shown here, so they are elided in favor of the
> contracts and registries that wire them together.

## Package overview

```mermaid
classDiagram
    direction TB

    class Core["PaycheckCalc.Core"] {
        <<library>>
        UI-agnostic tax + budget engine
    }
    class Shared["PaycheckCalc.Shared"] {
        <<library>>
        Sync contracts, JSON, merge, HTTP client
    }
    class App["PaycheckCalc.App (MAUI)"] {
        <<head>>
        Android & Windows MVVM
    }
    class Blazor["PaycheckCalc.Blazor"] {
        <<head>>
        Blazor Server web app
    }
    class Api["PaycheckCalc.Api"] {
        <<service>>
        Identity accounts + paycheck/budget sync (PostgreSQL)
    }
    class Tests["PaycheckCalc.Tests"] {
        <<xUnit>>
    }

    Shared ..> Core : ProjectReference
    App ..> Core : ProjectReference
    App ..> Shared : ProjectReference
    Blazor ..> Core : ProjectReference
    Blazor ..> Shared : ProjectReference
    Api ..> Shared : ProjectReference
    Tests ..> Core : ProjectReference
    Tests ..> Shared : ProjectReference
    Tests ..> Api : ProjectReference
    Tests ..> Blazor : ProjectReference
    App ..> Api : HTTP (sync)
    Blazor ..> Api : HTTP (sync)
```

## Core — paycheck pipeline

```mermaid
classDiagram
    direction TB

    %% ── Domain models ───────────────────────────────────────
    class PaycheckInput {
        <<sealed>>
        +PayFrequency Frequency
        +PayType PayType
        +decimal HourlyRate
        +decimal RegularHours
        +decimal OvertimeHours
        +decimal OvertimeMultiplier
        +decimal SalaryAmount
        +SalaryBasis SalaryBasis
        +UsState State
        +StateInputValues? StateInputValues
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
        +decimal FicaTaxableWages
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +UsState State
        +decimal StateTaxableWages
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
        +string StateDisabilityInsuranceLabel
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

    class GrossUpCalculator {
        +Calculate(target, PaycheckInput) GrossUpResult
    }

    class GrossUpResult {
        <<sealed>>
        +decimal TargetNetPay
        +decimal GrossUpPay
        +PaycheckResult Paycheck
        +decimal GrossUpCost
        +bool Converged
    }

    %% ── Budget engine ───────────────────────────────────────
    class BudgetCalculator {
        +Calculate(Budget, transactions, today) BudgetSummary
    }

    class Budget {
        +string Name
        +decimal MonthlyNetIncome
        +IReadOnlyList~BudgetCategory~ Categories
    }

    class BudgetSummary {
        +decimal TotalBudgeted
        +decimal TotalSpent
        +IReadOnlyList~CategorySummary~ Categories
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
    PayCalculator --> FicaCalculator
    PayCalculator --> Irs15TPercentageCalculator
    PayCalculator ..> PaycheckInput
    PayCalculator ..> PaycheckResult
    AnnualProjectionCalculator --> Irs15TPercentageCalculator
    AnnualProjectionCalculator --> FicaCalculator
    AnnualProjectionCalculator ..> AnnualProjection
    GrossUpCalculator --> PayCalculator : re-runs at each probe
    GrossUpCalculator ..> GrossUpResult
    GrossUpResult o-- PaycheckResult
    BudgetCalculator ..> Budget
    BudgetCalculator ..> BudgetSummary
    StateCalculatorRegistry o-- IStateWithholdingCalculator : 51 registered
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

    class PaychecksPage {
        <<ContentPage>>
        Saved list + A/B comparison
    }

    class BudgetPage {
        <<ContentPage>>
        50/30/20, categories, transactions
    }

    class AccountPage {
        <<ContentPage>>
        Sign-in / sync / server URL
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

    class BudgetViewModel {
        +Categories / Transactions
        +Apply 50/30/20 + summary
    }

    class AccountViewModel {
        +sign-in / sync / server URL
    }

    class SavedPaycheckViewModel {
        wraps one saved paycheck
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
    PaychecksPage --> CalculatorViewModel : BindingContext
    BudgetPage --> BudgetViewModel : BindingContext
    AccountPage --> AccountViewModel : BindingContext
    ResultsPage --> DoughnutChartDrawable
    CalculatorViewModel o-- StateFieldViewModel
    CalculatorViewModel o-- DeductionItemViewModel
    CalculatorViewModel o-- SavedPaycheckViewModel
    CalculatorViewModel ..> PaycheckInputMapper
    CalculatorViewModel ..> ResultCardMapper
    CalculatorViewModel ..> AnnualProjectionMapper
    ResultCardMapper ..> ResultCardModel
    AnnualProjectionMapper ..> AnnualProjectionModel
    DoughnutChartDrawable ..> ResultCardModel
```

## Shared — accounts, sync & storage

```mermaid
classDiagram
    direction TB

    class PaycheckApiClient {
        +RegisterAsync() / LoginAsync() / LogoutAsync()
        +SyncAsync(SyncRequest) SyncResponse
        +SyncBudgetsAsync(BudgetSyncRequest) BudgetSyncResponse
    }

    class ITokenStore {
        <<interface>>
        +GetTokensAsync() / SetTokensAsync()
    }
    class IApiBaseAddressProvider {
        <<interface>>
        +Uri? BaseAddress
    }

    class ISavedPaycheckStore {
        <<interface>>
        +LoadAsync() / UpsertAsync() / RemoveAsync()
        +ReplaceAllAsync(SavedPaycheckSet)
    }
    class IBudgetStore {
        <<interface>>
        +Load/Upsert/Remove/ReplaceAll (budgets + transactions)
    }

    class PaycheckSyncService {
        +SyncAsync() SyncOutcome
    }
    class BudgetSyncService {
        +SyncAsync() BudgetSyncOutcome
    }

    class SavedPaycheckMerger {
        +Merge(existing, incoming) SavedPaycheckSet
    }
    class BudgetMerger {
        +MergeBudgets(...) / MergeTransactions(...)
    }

    class SyncDbContext {
        <<IdentityDbContext>>
        SavedPaychecks / Budgets / BudgetTransactions
    }

    PaycheckApiClient --> ITokenStore
    PaycheckApiClient --> IApiBaseAddressProvider
    PaycheckSyncService --> ISavedPaycheckStore
    PaycheckSyncService --> PaycheckApiClient
    BudgetSyncService --> IBudgetStore
    BudgetSyncService --> PaycheckApiClient
    PaycheckApiClient ..> SavedPaycheckMerger : server-side merge
    PaycheckApiClient ..> BudgetMerger : server-side merge
    SavedPaycheckMerger ..> SyncDbContext
    BudgetMerger ..> SyncDbContext
```

> Store implementations: MAUI persists to on-device JSON (`JsonFilePaycheckStore`,
> `JsonFileBudgetStore`); Blazor uses circuit memory (`SessionPaycheckStore`, `SessionBudgetStore`); the
> server persists EF Core rows in PostgreSQL. The mergers are defined once in `PaycheckCalc.Shared` and
> reused by the API and clients.
