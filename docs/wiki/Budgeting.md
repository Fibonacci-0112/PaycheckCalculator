# Budgeting

PaycheckCalc includes a monthly budget tracker built on the UI-agnostic Core layer.

The budget module can normalize paycheck net pay into monthly income, apply budget templates, track categories and expenses, track recurring bills, track savings goals, generate report data, and sync budget state across the MAUI and Blazor front-ends when an account is used.

---

## Core Domain

Budgeting lives in `PaycheckCalculator.Core/Budgeting/`.

| Type | Purpose |
|---|---|
| `Budget` | Named monthly budget with `MonthlyNetIncome`, `Categories`, and `Method`. |
| `BudgetCategory` | Category name, `BudgetType`, amount, and amount type. |
| `BudgetTransaction` | Dated expense with stable GUID, category, amount, and description. |
| `RecurringBill` | Recurring expense with stable GUID, name, category, amount, recurrence frequency, optional due day, and monthly equivalent. |
| `SavingsGoal` | Savings target with stable GUID, target amount, current amount, optional target date, remaining amount, and monthly contribution needed. |
| `BudgetCalculator` | Builds a `BudgetSummary` from a budget, transactions, optional recurring bills, optional savings goals, and today's date. |
| `BudgetSummary` | Monthly income, total budgeted, total spent, unallocated amount, remaining amount, recurring total, savings contribution total, full-allocation status, and category summaries. |
| `CategorySummary` | Per-category budgeted/spent/remaining/projected/recurring totals. |
| `MonthlyIncomeNormalizer` | Converts net pay per pay period into monthly net income using actual pay-frequency periods per year. |
| `BudgetReportCalculator` | Produces multi-month budget-vs-actual and spend-by-category report data. |

All monetary values use `decimal` and round to cents with `MidpointRounding.AwayFromZero`.

---

## Budget Methods

`BudgetMethod` is metadata used by the UI for templates and guidance.

| Method | Behavior |
|---|---|
| `FiftyThirtyTwenty` | Applies Needs 50%, Wants 30%, Savings 20%. |
| `ZeroBased` | Guides the user toward assigning every dollar of monthly net income. |
| `Envelope` | Supports envelope-style category planning. |
| `Custom` | Leaves categories fully user-defined. |

The method does not change the category math itself. Category math is still driven by `BudgetCategory.EffectiveMonthlyBudget`.

---

## Income Normalization

`MonthlyIncomeNormalizer.ToMonthly(netPerPeriod, frequency)` converts paycheck net pay into monthly income:

```text
monthly income = net per period × periods per year / 12
```

It uses `PayPeriods.PerYear`, so weekly and biweekly pay are not approximated with shortcut multipliers.

---

## Categories and Transactions

Categories define the plan. Transactions record actual spend.

`BudgetCalculator` groups transactions by category name using case-insensitive matching. For each category it computes budgeted amount, spent amount, remaining amount, recurring total, and projected month-end spending.

Projected month-end spending uses a simple linear run-rate:

```text
projected month-end = spent / day of month × days in month
```

---

## Recurring Bills

`RecurringBill` represents recurring expenses such as rent, utilities, subscriptions, and insurance.

Supported recurrence frequencies:

| Frequency | Recurrences/Year |
|---|---:|
| Weekly | 52 |
| Biweekly | 26 |
| Semimonthly | 24 |
| Monthly | 12 |
| Quarterly | 4 |
| Semiannual | 2 |
| Annual | 1 |

`RecurrencePeriods.MonthlyEquivalent(amount, frequency)` uses:

```text
monthly equivalent = amount × recurrences per year / 12
```

Recurring totals flow into `BudgetSummary.TotalRecurring` and each matching category's `CategorySummary.Recurring`.

---

## Savings Goals

`SavingsGoal` tracks progress toward a target amount.

It stores target amount, current amount, and an optional target date. `Remaining` is target minus current, never below zero.

`MonthlyContributionNeeded(today)` returns the monthly amount needed to reach the target date. It returns zero when the goal is already met or when no target date exists. If the target month is current or past, the full remaining amount is required this month.

Savings goal totals flow into `BudgetSummary.TotalSavingsContribution`.

---

## Budget Reports

`BudgetReportCalculator` produces read-only report data:

- `BudgetVsActualPoint` — one row per month comparing total budgeted and actual spending.
- `SpendByCategoryPoint` — one row per month/category pair.
- `BudgetReport` — aggregate report with covered months and generation date.

MAUI and Blazor include CSV/PDF report export paths. Report UI is controlled by `IEntitlementProvider`; the default implementation reports free-tier access until a paid entitlement implementation is added.

---

## Front-End Usage

### MAUI

`BudgetPage` and `BudgetViewModel` own the MAUI Budget tab.

The tab supports:

- Seeding monthly income from the latest paycheck result.
- Choosing a budget method.
- Applying method templates.
- Editing categories.
- Adding and removing transactions.
- Adding and removing recurring bills.
- Adding and removing savings goals.
- Viewing budgeted, spent, remaining, unallocated, recurring, and savings totals.
- Generating and exporting budget reports when available.

MAUI persists budget data through `JsonFileBudgetStore`.

### Blazor

`Budget.razor` mirrors the budget feature in the web app:

- Monthly income setup.
- Budgeting method picker.
- Category bars.
- Recurring bills table.
- Savings goals table.
- Expense entry and current-month expense list.
- Budget reports with CSV/PDF export when available.

Anonymous Blazor budget state lives in `SessionBudgetStore` for the lifetime of the circuit.

---

## Sync Model

Budget sync lives in `PaycheckCalculator.Shared/Budgeting/`.

The sync payload includes four independent collections:

| Collection | Live DTO | Removal record | Identity |
|---|---|---|---|
| Budgets | `BudgetDto` | `BudgetTombstone` | Case-insensitive budget name |
| Transactions | `TransactionDto` | `TransactionTombstone` | GUID |
| Recurring bills | `RecurringBillDto` | `RecurringBillTombstone` | GUID |
| Savings goals | `SavingsGoalDto` | `SavingsGoalTombstone` | GUID |

`BudgetSyncRequest` sends all four collections to the server. `BudgetSyncResponse` returns the merged state. Clients replace their local state with the returned state.

`BudgetMerger` applies deterministic last-write-wins behavior to each collection:

1. Later timestamp wins.
2. On exact timestamp ties, a live entry beats a removal record.
3. On exact same-kind ties, the incoming side wins.

The API persists budget sync rows in PostgreSQL through `BudgetEntity`, `BudgetTransactionEntity`, `RecurringBillEntity`, and `SavingsGoalEntity`.

---

## Testing

Relevant test areas include:

- Budget calculation and category projections.
- Recurring bill monthly-equivalent conversion.
- Savings goal monthly contribution calculations.
- Budget report aggregation.
- Budget merge behavior.
- Sync API round-trips.
