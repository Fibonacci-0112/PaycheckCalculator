# UML Class Diagram

> High-level Mermaid class diagrams for the current **PaycheckCalc** solution.
>
> These diagrams are architectural rather than exhaustive. State calculators are represented by their shared contracts and registry instead of listing every state class.

## Package Overview

```mermaid
classDiagram
    direction TB

    class Core["PaycheckCalc.Core"] {
        <<library>>
        tax + pay + budget + report engine
    }
    class Shared["PaycheckCalc.Shared"] {
        <<library>>
        DTOs + JSON + merge + API client + stores + entitlements
    }
    class App["PaycheckCalc.App"] {
        <<MAUI head>>
        Android + Windows MVVM
    }
    class Blazor["PaycheckCalc.Blazor"] {
        <<web head>>
        Blazor Server
    }
    class Api["PaycheckCalc.Api"] {
        <<service>>
        Identity + sync API + PostgreSQL
    }
    class Tests["PaycheckCalc.Tests"] {
        <<xUnit>>
        Core + Shared + Api + Blazor tests
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
    App ..> Api : HTTP sync
    Blazor ..> Api : HTTP sync
```

## Core — Paycheck Pipeline

```mermaid
classDiagram
    direction TB

    class PaycheckInput {
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
        +decimal GrossPay
        +decimal FederalTaxableIncome
        +decimal FicaTaxableWages
        +decimal StateTaxableWages
        +decimal FederalWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +decimal StateWithholding
        +decimal StateDisabilityInsurance
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
        +Calculate(...) GrossUpResult
    }
    class AnnualProjection {
        +int PayPeriodsPerYear
        +int CurrentPaycheckNumber
        +int RemainingPaychecks
        +decimal AnnualizedGrossPay
        +decimal AnnualizedNetPay
        +decimal OverUnderWithholding
    }
    class GrossUpResult {
        +decimal TargetNetPay
        +decimal GrossUpPay
        +decimal GrossUpCost
        +bool Converged
        +PaycheckResult Paycheck
    }

    PayCalculator --> StateCalculatorRegistry
    PayCalculator --> FicaCalculator
    PayCalculator --> Irs15TPercentageCalculator
    PayCalculator ..> PaycheckInput
    PayCalculator ..> PaycheckResult
    AnnualProjectionCalculator --> Irs15TPercentageCalculator
    AnnualProjectionCalculator --> FicaCalculator
    AnnualProjectionCalculator ..> AnnualProjection
    GrossUpCalculator --> PayCalculator : re-runs pipeline
    GrossUpCalculator ..> GrossUpResult
    PaycheckInput o-- Deduction
    GrossUpResult o-- PaycheckResult
```

## Core — State Tax Plugin Model

```mermaid
classDiagram
    direction TB

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
        +GetSchema(UsState) IReadOnlyList~StateFieldDefinition~
    }

    class StateFieldDefinition {
        +string Key
        +string Label
        +StateFieldType FieldType
        +bool Required
    }

    class StateInputValues {
        +GetString(key) string?
        +GetBool(key) bool
        +GetInt32(key) int
        +GetDecimal(key) decimal
    }

    class StateWithholdingResult {
        +decimal TaxableWages
        +decimal Withholding
        +decimal DisabilityInsurance
        +string DisabilityInsuranceLabel
    }

    StateCalculatorRegistry o-- IStateWithholdingCalculator : registered calculators
    IStateWithholdingCalculator ..> StateInputValues
    IStateWithholdingCalculator ..> StateWithholdingResult
    IStateWithholdingCalculator ..> StateFieldDefinition
    JsonStateSchemaProvider ..|> IStateSchemaProvider
    JsonStateSchemaProvider ..> StateFieldDefinition
```

## Core — Budgeting and Reports

```mermaid
classDiagram
    direction TB

    class Budget {
        +string Name
        +decimal MonthlyNetIncome
        +BudgetMethod Method
        +IReadOnlyList~BudgetCategory~ Categories
    }
    class BudgetCategory {
        +string Name
        +BudgetType BudgetType
        +decimal Amount
        +BudgetAmountType AmountType
        +EffectiveMonthlyBudget(decimal) decimal
    }
    class BudgetTransaction {
        +Guid Id
        +string CategoryName
        +decimal Amount
        +DateOnly Date
        +string Description
    }
    class RecurringBill {
        +Guid Id
        +string Name
        +string CategoryName
        +decimal Amount
        +RecurrenceFrequency Frequency
        +int? DueDayOfMonth
        +decimal MonthlyEquivalent
    }
    class SavingsGoal {
        +Guid Id
        +string Name
        +decimal TargetAmount
        +decimal CurrentAmount
        +DateOnly? TargetDate
        +decimal Remaining
        +MonthlyContributionNeeded(DateOnly) decimal
    }
    class BudgetCalculator {
        +Calculate(Budget, transactions, today, bills, goals) BudgetSummary
    }
    class BudgetSummary {
        +decimal MonthlyNetIncome
        +decimal TotalBudgeted
        +decimal TotalSpent
        +decimal TotalRecurring
        +decimal TotalSavingsContribution
        +decimal Unallocated
        +bool IsFullyAllocated
        +decimal Remaining
    }
    class CategorySummary {
        +string Name
        +decimal Budgeted
        +decimal Spent
        +decimal Recurring
        +decimal Remaining
        +decimal ProjectedMonthEnd
    }
    class BudgetReportCalculator {
        +Compute(Budget, transactions, through, monthsBack) BudgetReport
    }
    class BudgetReport {
        +string BudgetName
        +IReadOnlyList~DateOnly~ Months
        +IReadOnlyList~SpendByCategoryPoint~ SpendByCategory
        +IReadOnlyList~BudgetVsActualPoint~ BudgetVsActual
        +DateOnly GeneratedThrough
    }
    class RecurrencePeriods {
        +PerYear(RecurrenceFrequency) int
        +MonthlyEquivalent(decimal, RecurrenceFrequency) decimal
    }

    Budget o-- BudgetCategory
    BudgetCalculator ..> Budget
    BudgetCalculator ..> BudgetTransaction
    BudgetCalculator ..> RecurringBill
    BudgetCalculator ..> SavingsGoal
    BudgetCalculator ..> BudgetSummary
    BudgetSummary o-- CategorySummary
    BudgetReportCalculator ..> Budget
    BudgetReportCalculator ..> BudgetTransaction
    BudgetReportCalculator ..> BudgetReport
    RecurringBill ..> RecurrencePeriods
```

## Core — Explanations

```mermaid
classDiagram
    direction TB

    class PaycheckExplanation {
        +Get(ExplanationLineKey) LineExplanation?
    }
    class LineExplanation {
        +string Title
        +decimal FinalAmount
        +IReadOnlyList~ExplanationStep~ Steps
        +string? Reference
    }
    class ExplanationStep {
        +string Label
        +string Formula
        +decimal? Amount
    }

    PaycheckResult o-- PaycheckExplanation
    PaycheckExplanation o-- LineExplanation
    LineExplanation o-- ExplanationStep
```

## MAUI Head — MVVM Layer

```mermaid
classDiagram
    direction TB

    class InputsPage
    class ResultsPage
    class PaychecksPage
    class BudgetPage
    class AccountPage

    class CalculatorViewModel {
        +CalculateCommand
        +ShowExplanationCommand
        +SavePaycheckCommand
        +Compare selected paychecks
        +ResultCardModel? ResultCard
        +AnnualProjectionModel? Projection
    }
    class BudgetViewModel {
        +BudgetMethod Method
        +Categories
        +Transactions
        +RecurringBills
        +SavingsGoals
        +BudgetReport? Report
        +ApplyMethodTemplateCommand
        +GenerateReportCommand
    }
    class AccountViewModel {
        +SignInCommand
        +CreateAccountCommand
        +SyncCommand
        +SignOutCommand
    }
    class StateFieldViewModel
    class DeductionItemViewModel
    class SavedPaycheckViewModel
    class RecurringBillViewModel
    class SavingsGoalViewModel
    class PaycheckInputMapper
    class ResultCardMapper
    class AnnualProjectionMapper
    class DoughnutChartDrawable

    InputsPage --> CalculatorViewModel : BindingContext
    ResultsPage --> CalculatorViewModel : BindingContext
    PaychecksPage --> CalculatorViewModel : BindingContext
    BudgetPage --> BudgetViewModel : BindingContext
    AccountPage --> AccountViewModel : BindingContext
    CalculatorViewModel o-- StateFieldViewModel
    CalculatorViewModel o-- DeductionItemViewModel
    CalculatorViewModel o-- SavedPaycheckViewModel
    BudgetViewModel o-- RecurringBillViewModel
    BudgetViewModel o-- SavingsGoalViewModel
    CalculatorViewModel ..> PaycheckInputMapper
    CalculatorViewModel ..> ResultCardMapper
    CalculatorViewModel ..> AnnualProjectionMapper
    ResultsPage --> DoughnutChartDrawable
```

## Blazor Head — Server UI Layer

```mermaid
classDiagram
    direction TB

    class CalculatorRazor["Calculator.razor"] {
        paycheck calculator UI
    }
    class BudgetRazor["Budget.razor"] {
        budget UI + reports
    }
    class HomeRazor["Home.razor"]
    class StateLandingPage
    class DoughnutChart
    class ExplanationModal
    class SessionPaycheckStore
    class SessionBudgetStore
    class StateMetadata
    class FileSystemTaxDataReader
    class PaycheckExportService
    class BudgetReportCsvRenderer
    class BudgetReportPdfRenderer
    class CircuitAccountSession

    CalculatorRazor --> PayCalculator
    CalculatorRazor --> GrossUpCalculator
    CalculatorRazor --> AnnualProjectionCalculator
    CalculatorRazor --> SessionPaycheckStore
    CalculatorRazor --> DoughnutChart
    CalculatorRazor --> ExplanationModal
    BudgetRazor --> BudgetCalculator
    BudgetRazor --> BudgetReportCalculator
    BudgetRazor --> SessionBudgetStore
    BudgetRazor --> BudgetReportCsvRenderer
    BudgetRazor --> BudgetReportPdfRenderer
    StateLandingPage --> StateMetadata
    FileSystemTaxDataReader ..> Core : TaxData files
    CircuitAccountSession ..> PaycheckApiClient
```

## Shared — Sync, Stores, Client, Entitlements

```mermaid
classDiagram
    direction TB

    class PaycheckApiClient {
        +RegisterAsync()
        +LoginAsync()
        +SyncAsync(SyncRequest) SyncResponse
        +SyncBudgetsAsync(BudgetSyncRequest) BudgetSyncResponse
    }
    class ISavedPaycheckStore {
        <<interface>>
        +LoadAsync()
        +UpsertAsync()
        +RemoveAsync()
        +ReplaceAllAsync()
    }
    class IBudgetStore {
        <<interface>>
        +LoadBudgetsAsync()
        +LoadTransactionsAsync()
        +LoadRecurringBillsAsync()
        +LoadSavingsGoalsAsync()
        +ReplaceAll...Async()
    }
    class SavedPaycheckMerger {
        +Merge(existing, incoming) SavedPaycheckSet
    }
    class BudgetMerger {
        +MergeBudgets(...)
        +MergeTransactions(...)
        +MergeRecurringBills(...)
        +MergeSavingsGoals(...)
    }
    class PaycheckSyncService {
        +SyncAsync() SyncOutcome
    }
    class BudgetSyncService {
        +SyncAsync() BudgetSyncOutcome
    }
    class PaycheckJson {
        +Options JsonSerializerOptions
        +AddConverters(options)
    }
    class IEntitlementProvider {
        <<interface>>
        +bool IsPro
    }
    class FreeEntitlementProvider {
        +bool IsPro
    }

    PaycheckSyncService --> ISavedPaycheckStore
    PaycheckSyncService --> PaycheckApiClient
    BudgetSyncService --> IBudgetStore
    BudgetSyncService --> PaycheckApiClient
    PaycheckApiClient ..> PaycheckJson
    SavedPaycheckMerger ..> PaycheckJson
    BudgetMerger ..> PaycheckJson
    FreeEntitlementProvider ..|> IEntitlementProvider
```

## API — Persistence and Endpoints

```mermaid
classDiagram
    direction TB

    class Program {
        MapIdentityApi
        MapPaycheckSyncEndpoints
        MapBudgetSyncEndpoints
    }
    class SyncDbContext {
        <<IdentityDbContext>>
        +DbSet~SavedPaycheckEntity~ SavedPaychecks
        +DbSet~BudgetEntity~ Budgets
        +DbSet~BudgetTransactionEntity~ BudgetTransactions
        +DbSet~RecurringBillEntity~ RecurringBills
        +DbSet~SavingsGoalEntity~ SavingsGoals
    }
    class PaycheckSyncEndpoints {
        +POST /api/paychecks/sync
        +GET /api/paychecks/
    }
    class BudgetSyncEndpoints {
        +POST /api/budgets/sync
        +GET /api/budgets/
    }
    class SavedPaycheckEntity
    class BudgetEntity
    class BudgetTransactionEntity
    class RecurringBillEntity
    class SavingsGoalEntity

    Program --> SyncDbContext
    Program --> PaycheckSyncEndpoints
    Program --> BudgetSyncEndpoints
    PaycheckSyncEndpoints --> SyncDbContext
    PaycheckSyncEndpoints --> SavedPaycheckMerger
    BudgetSyncEndpoints --> SyncDbContext
    BudgetSyncEndpoints --> BudgetMerger
    SyncDbContext o-- SavedPaycheckEntity
    SyncDbContext o-- BudgetEntity
    SyncDbContext o-- BudgetTransactionEntity
    SyncDbContext o-- RecurringBillEntity
    SyncDbContext o-- SavingsGoalEntity
```
