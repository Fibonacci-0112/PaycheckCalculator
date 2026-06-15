# Budgeting

PaycheckCalc includes a lightweight **monthly budget tracker** built on the same UI-agnostic Core engine.
It lets you turn a paycheck's net pay into a monthly budget (with an optional 50/30/20 preset), record
expenses against categories, and watch budgeted vs. spent vs. a projected month-end. Budgets and
transactions persist locally and can sync across the MAUI and Blazor front-ends with an account.

---

## Core domain (`PaycheckCalc.Core/Budgeting/`)

| Type | Purpose |
|---|---|
| `Budget` | A named budget: `Name`, `MonthlyNetIncome`, and a list of `BudgetCategory`. |
| `BudgetCategory` | `Name`, `BudgetType` (Needs / Wants / Savings), `Amount`, and `AmountType` (Dollar or Percentage). `EffectiveMonthlyBudget(monthlyNetIncome)` returns the dollar value or `Amount/100 × income`, rounded to the cent. |
| `BudgetTransaction` | A recorded expense: stable `Id` (GUID), `CategoryName`, `Amount`, `Date` (`DateOnly`), `Description`. |
| `BudgetCalculator` | Computes a `BudgetSummary` from a `Budget`, its month's transactions, and `today`. Sums spend per category (case-insensitive name match) and projects month-end by **linear run-rate**: `spent / dayOfMonth × daysInMonth`. |
| `BudgetSummary` / `CategorySummary` | The result: `MonthlyNetIncome`, `TotalBudgeted`, `TotalSpent`, `Unallocated` (income − budgeted), `Remaining` (budgeted − spent), and a per-category breakdown (budgeted, spent, remaining, projected month-end). |
| `AllocationRules` | `FiftyThirtyTwenty()` returns Needs 50% / Wants 30% / Savings 20% percentage categories. |
| `MonthlyIncomeNormalizer` | Converts per-period net pay to a monthly figure: `netPerPeriod × periodsPerYear / 12` (uses the real `PayPeriods.PerYear`, never assumes ×4). |
| `BudgetType` / `BudgetAmountType` | Enums: `Needs`/`Wants`/`Savings` and `Dollar`/`Percentage`. |

All money uses `decimal` and rounds to the cent with `MidpointRounding.AwayFromZero`, consistent with the
paycheck engine. `BudgetCalculator` is registered in DI by `AddPaycheckCalcCore`.

---

## Front-end usage

### MAUI (Budget tab)

`BudgetPage` / `BudgetViewModel` (with `BudgetCategoryViewModel` and `BudgetTransactionViewModel`) own the
UI. You can seed monthly income from the current paycheck (via `MonthlyIncomeNormalizer`), apply the
50/30/20 preset, edit categories, and add/remove transactions. State persists on device through
`JsonFileBudgetStore` → `budgets.json`.

### Blazor (`/budget`)

`Budget.razor` mirrors the MAUI page: income setup, 50/30/20 preset, category bars (color-coded by
spend-to-budget ratio), an add-expense form, and a transaction list. Anonymous state lives in
`SessionBudgetStore` (circuit memory) until the tab closes.

---

## Sync

Budgets and transactions sync through the same account/HTTP path as paychecks. The shared layer
(`PaycheckCalc.Shared/Budgeting/`) defines the DTOs (`BudgetDto`, `BudgetCategoryDto`, `TransactionDto`),
the sets and tombstones, the `IBudgetStore` abstraction, the deterministic `BudgetMerger`
(budgets keyed by case-insensitive name, transactions by GUID; last-write-wins with tombstones), and
`BudgetSyncService`. The server persists `BudgetEntity` and `BudgetTransactionEntity` rows and exposes
`/api/budgets/sync` and `GET /api/budgets/`. See [Accounts & Sync](Accounts-and-Sync.md) for the full sync
design, merge semantics, and storage details.

---

## Testing

`BudgetCalculatorTest` covers allocation, per-category spend aggregation, and the month-end projection;
`BudgetMergerTest` covers the last-write-wins merge and tombstone propagation for budgets and transactions.
