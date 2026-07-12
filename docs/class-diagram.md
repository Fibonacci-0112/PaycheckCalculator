# UML Class Diagram

> High-level Mermaid class diagrams for the current **PaycheckCalc** solution.
>
> These diagrams are architectural rather than exhaustive. State calculators are represented by their shared contracts and registry instead of listing every state class.

## Package Overview

```mermaid
classDiagram
    direction TB

    class Core
    class Shared
    class App
    class Blazor
    class Api
    class Tests

    <<library>> Core
    <<library>> Shared
    <<MAUI>> App
    <<BlazorServer>> Blazor
    <<WebAPI>> Api
    <<xUnit>> Tests

    note for Core "PaycheckCalc.Core: tax, pay, gross-up, projection, budget, and report engine"
    note for Shared "PaycheckCalc.Shared: DTOs, JSON, merge, API client, stores, entitlements"
    note for App "PaycheckCalc.App: Android and Windows MVVM frontend"
    note for Blazor "PaycheckCalc.Blazor: Blazor Server frontend"
    note for Api "PaycheckCalc.Api: Identity, sync endpoints, EF Core PostgreSQL"
    note for Tests "PaycheckCalc.Tests: Core, Shared, Api, and Blazor tests"

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
        +StateInputValues StateInputValues
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
        +Calculate() FicaResult
    }
    class Irs15TPercentageCalculator {
        +Calculate() decimal
    }
    class AnnualProjectionCalculator {
        +Calculate(PaycheckInput, PaycheckResult) AnnualProjection
    }
    class GrossUpCalculator {
        +Calculate() GrossUpResult
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
    class SelfEmploymentCalculator {
        +Calculate(SelfEmploymentInput) SelfEmploymentResult
    }
    class SelfEmploymentInput {
        +decimal AnnualNetEarnings
        +UsState State
        +decimal YtdSocialSecurityWages
        +decimal YtdMedicareWages
    }
    class SelfEmploymentResult {
        +decimal AnnualNetEarnings
        +decimal NetEarningsSubjectToSeTax
        +decimal SelfEmploymentTax
        +decimal StateIncomeTax
        +decimal TakeHome
        +IReadOnlyList~QuarterlyEstimate~ QuarterlyEstimates
    }
    class QuarterlyEstimate {
        +string Label
        +DateOnly DueDate
        +decimal FederalAmount
        +decimal StateAmount
        +decimal TotalAmount
    }
    class FederalSupplementalCalculator {
        +Calculate(decimal bonusAmount, decimal ytdSupplemental) decimal
    }
    class StateSupplementalCalculator {
        +Calculate(UsState, decimal bonusAmount) decimal
    }
    class BonusCalculator {
        +Calculate(BonusInput) BonusResult
    }
    class BonusInput {
        +decimal BonusAmount
        +UsState State
        +decimal YtdSupplementalWages
        +decimal YtdSocialSecurityWages
        +decimal YtdMedicareWages
    }
    class BonusResult {
        +decimal BonusAmount
        +decimal FederalWithholding
        +decimal SocialSecurityWithholding
        +decimal MedicareWithholding
        +decimal AdditionalMedicareWithholding
        +decimal StateWithholding
        +bool StateUsesRegularMethod
        +decimal NetBonus
        +PaycheckExplanation Explanation
    }
    class HourlySalaryCalculator {
        +Convert(HourlySalaryInput) HourlySalaryResult
    }
    class HourlySalaryInput {
        +PayConversionMode Mode
        +decimal HourlyRate
        +decimal AnnualSalary
        +decimal HoursPerWeek
        +decimal WeeksPerYear
        +PayFrequency Frequency
    }
    class HourlySalaryResult {
        +decimal HourlyRate
        +decimal AnnualSalary
        +decimal HoursPerYear
        +decimal PerPeriodPay
        +decimal WeeklyPay
        +decimal BiweeklyPay
        +decimal SemimonthlyPay
        +decimal MonthlyPay
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
    SelfEmploymentCalculator --> StateCalculatorRegistry : state income-tax estimate
    SelfEmploymentCalculator --> FicaCalculator : SS wage base / threshold
    SelfEmploymentCalculator ..> SelfEmploymentInput
    SelfEmploymentCalculator ..> SelfEmploymentResult
    SelfEmploymentResult o-- QuarterlyEstimate
    BonusCalculator --> FederalSupplementalCalculator
    BonusCalculator --> FicaCalculator
    BonusCalculator --> StateSupplementalCalculator
    BonusCalculator ..> BonusInput
    BonusCalculator ..> BonusResult
    HourlySalaryCalculator ..> HourlySalaryInput
    HourlySalaryCalculator ..> HourlySalaryResult
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
        +GetString(string) string
        +GetBool(string) bool
        +GetInt32(string) int
        +GetDecimal(string) decimal
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
        +int DueDayOfMonth
        +decimal MonthlyEquivalent
    }
    class SavingsGoal {
        +Guid Id
        +string Name
        +decimal TargetAmount
        +decimal CurrentAmount
        +DateOnly TargetDate
        +decimal Remaining
        +MonthlyContributionNeeded(DateOnly) decimal
    }
    class BudgetCalculator {
        +Calculate(Budget, IReadOnlyList, DateOnly, IReadOnlyList, IReadOnlyList) BudgetSummary
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
        +Compute(Budget, IReadOnlyList, DateOnly, int) BudgetReport
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
        +Get(ExplanationLineKey) LineExplanation
    }
    class LineExplanation {
        +string Title
        +decimal FinalAmount
        +IReadOnlyList~ExplanationStep~ Steps
        +string Reference
    }
    class ExplanationStep {
        +string Label
        +string Formula
        +decimal Amount
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
        +CalculationMode CalculationMode
        +ResultCardModel ResultCard
        +AnnualProjectionModel Projection
    }
    class BudgetViewModel {
        +BudgetMethod Method
        +ObservableCollection Categories
        +ObservableCollection Transactions
        +ObservableCollection RecurringBills
        +ObservableCollection SavingsGoals
        +BudgetReport Report
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
    class BudgetCategoryViewModel
    class BudgetTransactionViewModel
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
    BudgetViewModel o-- BudgetCategoryViewModel
    BudgetViewModel o-- BudgetTransactionViewModel
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

    class CalculatorPage
    class BudgetPageRazor
    class HomePage
    class StateLandingPage
    class DoughnutChart
    class ExplanationModal
    class SessionPaycheckStore
    class SessionBudgetStore
    class StateMetadata
    class FileSystemTaxDataReader
    class PaycheckCsvRenderer
    class PaycheckPdfRenderer
    class BudgetReportCsvRenderer
    class BudgetReportPdfRenderer
    class CircuitAccountSession

    note for CalculatorPage "Calculator.razor"
    note for BudgetPageRazor "Budget.razor"

    CalculatorPage --> PayCalculator
    CalculatorPage --> GrossUpCalculator
    CalculatorPage --> BonusCalculator
    CalculatorPage --> SelfEmploymentCalculator
    CalculatorPage --> AnnualProjectionCalculator
    CalculatorPage --> SessionPaycheckStore
    CalculatorPage --> DoughnutChart
    CalculatorPage --> ExplanationModal
    CalculatorPage --> PaycheckCsvRenderer
    CalculatorPage --> PaycheckPdfRenderer
    BudgetPageRazor --> BudgetCalculator
    BudgetPageRazor --> BudgetReportCalculator
    BudgetPageRazor --> SessionBudgetStore
    BudgetPageRazor --> BudgetReportCsvRenderer
    BudgetPageRazor --> BudgetReportPdfRenderer
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
        +ReplaceAllBudgetsAsync()
        +ReplaceAllTransactionsAsync()
        +ReplaceAllRecurringBillsAsync()
        +ReplaceAllSavingsGoalsAsync()
    }
    class SavedPaycheckMerger {
        +Merge(SavedPaycheckSet, SavedPaycheckSet) SavedPaycheckSet
    }
    class BudgetMerger {
        +MergeBudgets(BudgetSet, BudgetSet) BudgetSet
        +MergeTransactions(TransactionSet, TransactionSet) TransactionSet
        +MergeRecurringBills(RecurringBillSet, RecurringBillSet) RecurringBillSet
        +MergeSavingsGoals(SavingsGoalSet, SavingsGoalSet) SavingsGoalSet
    }
    class PaycheckSyncService {
        +SyncAsync() SyncOutcome
    }
    class BudgetSyncService {
        +SyncAsync() BudgetSyncOutcome
    }
    class PaycheckJson {
        +Options JsonSerializerOptions
        +AddConverters(JsonSerializerOptions)
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

    class Program
    class SyncDbContext {
        <<IdentityDbContext>>
        +DbSet~SavedPaycheckEntity~ SavedPaychecks
        +DbSet~BudgetEntity~ Budgets
        +DbSet~BudgetTransactionEntity~ BudgetTransactions
        +DbSet~RecurringBillEntity~ RecurringBills
        +DbSet~SavingsGoalEntity~ SavingsGoals
    }
    class PaycheckSyncEndpoints {
        +MapPaycheckSyncEndpoints(RouteGroupBuilder) RouteGroupBuilder
    }
    class BudgetSyncEndpoints {
        +MapBudgetSyncEndpoints(RouteGroupBuilder) RouteGroupBuilder
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
