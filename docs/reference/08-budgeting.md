# 08 — Budgeting

A monthly budget tracker layered on top of the paycheck engine: allocate net income to
categories, log expenses against them, fold in recurring bills and savings goals, and (Pro-gated)
generate a multi-month spend report. All pure calculation lives in `Core/Budgeting/`; the sync
DTOs live in `Shared/Budgeting/`; both front-ends provide their own store and UI.

---

## Domain model (`Core/Budgeting/`)

### `Budget` and `BudgetCategory`

```csharp
public sealed class Budget
{
    public string Name { get; init; } = "";
    public IReadOnlyList<BudgetCategory> Categories { get; init; } = [];
    public decimal MonthlyNetIncome { get; init; }
    public BudgetMethod Method { get; init; } = BudgetMethod.Custom;   // UI guidance only
}

public sealed class BudgetCategory
{
    public string Name { get; init; } = "";
    public BudgetType BudgetType { get; init; }                        // Needs | Wants | Savings
    public decimal Amount { get; init; }
    public BudgetAmountType AmountType { get; init; } = BudgetAmountType.Dollar;

    public decimal EffectiveMonthlyBudget(decimal monthlyNetIncome) => AmountType switch
    {
        BudgetAmountType.Percentage => Math.Round(Amount / 100m * monthlyNetIncome, 2, MidpointRounding.AwayFromZero),
        _ => Amount
    };
}
```

`EffectiveMonthlyBudget` deliberately mirrors `Deduction.EffectiveAmount` — same shape, same
rounding, same idea: a percentage-based allocation scales automatically as income changes; a
dollar allocation does not.

`Budget.Method` is **descriptive only** — it drives which starter template and UI guidance are
shown, but never changes how `EffectiveMonthlyBudget` computes.

### `AllocationRules` — starter templates

```csharp
public static IReadOnlyList<BudgetCategory> FiftyThirtyTwenty() =>
[
    new() { Name = "Needs",   BudgetType = BudgetType.Needs,   Amount = 50m, AmountType = BudgetAmountType.Percentage },
    new() { Name = "Wants",   BudgetType = BudgetType.Wants,   Amount = 30m, AmountType = BudgetAmountType.Percentage },
    new() { Name = "Savings", BudgetType = BudgetType.Savings, Amount = 20m, AmountType = BudgetAmountType.Percentage }
];

public static IReadOnlyList<BudgetCategory> ZeroBasedStarter() =>
[
    // Housing, Food, Transportation, Utilities — Needs, $0 Dollar
    // Discretionary — Wants, $0 Dollar
    // Savings — Savings, $0 Dollar
];

public static IReadOnlyList<BudgetCategory> ForMethod(BudgetMethod method) => method switch
{
    BudgetMethod.FiftyThirtyTwenty => FiftyThirtyTwenty(),
    BudgetMethod.ZeroBased or BudgetMethod.Envelope => ZeroBasedStarter(),
    _ => []
};
```

The classic **50/30/20** rule seeds three percentage categories that always sum to 100% and
rescale with income automatically. **Zero-based** and **envelope** both start from the same
dollar-amount category set, seeded at $0 — the point of a zero-based budget is for
`BudgetSummary.Unallocated` to reach exactly zero as the user assigns every dollar. **Custom**
seeds nothing; the user builds categories manually.

### `BudgetTransaction`, `RecurringBill`, `SavingsGoal`

```csharp
public sealed class BudgetTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();     // stable sync key
    public string CategoryName { get; init; } = "";
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public string Description { get; init; } = "";
}
```

```csharp
public sealed class RecurringBill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public decimal Amount { get; init; }
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;
    public int? DueDayOfMonth { get; init; }             // informational, UI sorting/labels only

    public decimal MonthlyEquivalent => RecurrencePeriods.MonthlyEquivalent(Amount, Frequency);
}
```

`RecurrencePeriods` is the bill-side analogue of `PayPeriods`:

```csharp
public static int PerYear(RecurrenceFrequency frequency) => frequency switch
{
    Weekly => 52, Biweekly => 26, Semimonthly => 24, Monthly => 12,
    Quarterly => 4, Semiannual => 2, Annual => 1,
    _ => throw new ArgumentOutOfRangeException(...)
};

public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) =>
    Math.Round(amount * PerYear(frequency) / 12m, 2, MidpointRounding.AwayFromZero);
```

Explicitly "mirrors `PayPeriods.PerYear` so frequencies are never approximated with `× 4`" — a
weekly bill is `× 52 ÷ 12`, not `× 4`.

```csharp
public sealed class SavingsGoal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public DateOnly? TargetDate { get; init; }            // null = open-ended

    public decimal Remaining => Math.Max(0m, TargetAmount - CurrentAmount);

    public decimal MonthlyContributionNeeded(DateOnly today)
    {
        if (Remaining <= 0m) return 0m;
        if (TargetDate is not { } target) return 0m;

        var monthsRemaining = (target.Year - today.Year) * 12 + (target.Month - today.Month);
        if (monthsRemaining <= 0) return Remaining;         // due this month or overdue: pay it all now

        return Math.Round(Remaining / monthsRemaining, 2, MidpointRounding.AwayFromZero);
    }
}
```

The month-difference arithmetic (`(target.Year - today.Year) * 12 + (target.Month - today.Month)`)
correctly spans year boundaries without a day-based approximation. A goal whose target month has
already arrived or passed asks for the *entire* remaining balance this month rather than dividing
by zero or a negative count.

### `RecurringBill.CategoryName` / `SavingsGoal.Name` are plain strings, not FK references

Both link back to a category or budget by matching **string**, case-insensitively — same pattern
as everywhere else in this codebase (saved paychecks by name, budgets by name). There is no
referential-integrity enforcement; a renamed category simply stops matching its bills until
updated. This is a deliberate simplicity trade-off, consistent with the rest of the sync model.

---

## `BudgetCalculator` — the monthly summary

```csharp
public BudgetSummary Calculate(
    Budget budget,
    IReadOnlyList<BudgetTransaction> transactions,
    DateOnly today,
    IReadOnlyList<RecurringBill>? recurringBills = null,
    IReadOnlyList<SavingsGoal>? savingsGoals = null)
```

1. **Group transactions by category** (case-insensitive) and sum.
2. **Group recurring bills by category** and sum their `MonthlyEquivalent`.
3. **Project month-end per category** by linear run-rate extrapolation:

   ```csharp
   var projected = daysElapsed > 0
       ? Math.Round(spent / daysElapsed * daysInMonth, 2, MidpointRounding.AwayFromZero)
       : 0m;
   ```

   "If you keep spending at today's average daily rate, you'll end the month here." A naive
   linear projection, not a forecast model — and it says so implicitly by being simple.
4. **Total the required monthly savings-goal contribution** across all goals.

### `BudgetSummary`

```csharp
public decimal Unallocated => MonthlyNetIncome - TotalBudgeted;
public bool IsFullyAllocated => Math.Abs(Unallocated) <= 0.01m;   // 1-cent rounding tolerance
public decimal Remaining => TotalBudgeted - TotalSpent;
```

`IsFullyAllocated` is the zero-based budget's success signal, made robust to rounding rather than
requiring bit-exact equality.

### `CategorySummary`

Per category: `Budgeted`, `Spent`, `Remaining` (computed), `ProjectedMonthEnd`, and `Recurring`
(the monthly-equivalent cost of bills assigned to that category).

---

## `BudgetReportCalculator` — the multi-month report

Pro-gated in both front-ends (see below). Aggregates transaction history into two series over a
window of calendar months:

```csharp
public BudgetReport Compute(
    Budget budget,
    IReadOnlyList<BudgetTransaction> allTransactions,
    DateOnly through,
    int monthsBack = 5)
```

Builds the inclusive month range `[through.AddMonths(-monthsBack), through]`, then for each month:

- **`SpendByCategoryPoint`** — one row per `(month, category)` pair, every category represented
  even with zero spend, so a chart never has a silent gap.
- **`BudgetVsActualPoint`** — one row per month: total budgeted vs. total actual, with
  `Variance => Actual − Budgeted` computed (positive = over budget).

```csharp
public sealed class BudgetReport
{
    public string BudgetName { get; init; } = "";
    public IReadOnlyList<DateOnly> Months { get; init; } = [];
    public IReadOnlyList<SpendByCategoryPoint> SpendByCategory { get; init; } = [];
    public IReadOnlyList<BudgetVsActualPoint> BudgetVsActual { get; init; } = [];
    public DateOnly GeneratedThrough { get; init; }
}
```

---

## `MonthlyIncomeNormalizer`

Converts a per-period net-pay figure — the number the paycheck calculator actually produces —
into the monthly figure the budget engine needs:

```csharp
public static decimal ToMonthly(decimal netPayPerPeriod, PayFrequency frequency) =>
    Math.Round(netPayPerPeriod * PayPeriods.PerYear(frequency) / 12m, 2, MidpointRounding.AwayFromZero);
```

Reuses `PayPeriods.PerYear`, so every frequency including `Weekly53`/`Biweekly27` normalizes
correctly instead of a naive "biweekly × 2.167" approximation. This is the bridge from the
paycheck calculator's result to the budget's monthly-income input — see
`BudgetViewModel.ApplyMethodTemplate`, which seeds a budget's income directly from
`CalculatorViewModel.ResultCard`.

---

## Sync model (`Shared/Budgeting/`)

Four independently-synced collections, each following the exact same pattern already established
for saved paychecks (see [10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md)):

| DTO | Sync key | Tombstone |
|---|---|---|
| `BudgetDto` | `Name` (case-insensitive) | `BudgetTombstone(Name, DeletedAtUtc)` |
| `TransactionDto` | `Id` (GUID) | `TransactionTombstone(Id, DeletedAtUtc)` |
| `RecurringBillDto` | `Id` (GUID) | `RecurringBillTombstone(Id, DeletedAtUtc)` |
| `SavingsGoalDto` | `Id` (GUID) | `SavingsGoalTombstone(Id, DeletedAtUtc)` |

`BudgetDto.SchemaVersion` is `2` — it notes that schema version 2 (a project milestone referred to
as "C2" in comments) added the `Method` field, while recurring bills and savings goals were
introduced as their own GUID-keyed sync sets rather than nested inside the budget DTO.

`BudgetMerger` implements the merge for all four collections with the identical three-level
tie-break used by `SavedPaycheckMerger` — later timestamp wins; on an exact tie, a live entry
beats a tombstone; on a same-kind tie, the incoming side wins (making re-sync idempotent). It is
literally four parallel copies of the same generic algorithm, one per DTO type, because C# has no
shared-key-type generic that would collapse `string`-keyed and `Guid`-keyed merges into one
implementation without losing type safety.

`IBudgetStore` is the storage abstraction — sixteen methods, four CRUD-ish operations
(`Load`/`Upsert`/`Remove`/`ReplaceAll`) × four collections. `BudgetSyncService.SyncAsync` loads
all four from the store, pushes them in one `BudgetSyncRequest`, and replaces all four from the
single merged `BudgetSyncResponse` — one round trip syncs the whole budgeting feature.

---

## Store implementations

| Host | Class | Backing |
|---|---|---|
| MAUI | `JsonFileBudgetStore` | `budgets.json` in `FileSystem.AppDataDirectory`, semaphore-guarded, corrupt file moved aside rather than crashing startup — same pattern as `JsonFilePaycheckStore` |
| Blazor | `SessionBudgetStore` | In-memory dictionaries scoped to the circuit, mirrored to browser `localStorage` (`paycheckcalc.budgets.v1` — one envelope for all four domains) so they survive a refresh or a closed tab |

---

## Entitlements and Pro gating

`IEntitlementProvider` (Shared) is the single gate:

```csharp
public interface IEntitlementProvider { bool IsPro { get; } }

public sealed class FreeEntitlementProvider : IEntitlementProvider
{
    public bool IsPro => false;   // default: everyone is on the free tier
}
```

Registered as the sole implementation in both front-ends today — **report generation UI is
present but always gated off** until a paid entitlement provider is wired in (a placeholder for
future billing integration, referred to in comments as "E2").

MAUI checks it directly in the view model:

```csharp
[RelayCommand]
private void GenerateReport()
{
    if (!_entitlements.IsPro || MonthlyNetIncome <= 0m || Categories.Count == 0) return;
    ...
}
```

and in XAML, showing an upsell card when `!IsPro` and the real report when `IsPro`:

```xml
<Border IsVisible="{Binding IsPro, Converter={StaticResource InvertBool}}"> <!-- upsell --> </Border>
<Border IsVisible="{Binding IsPro}">                                        <!-- report --> </Border>
```

Blazor mirrors the same `@if (!Entitlements.IsPro)` branch in `Budget.razor`.

---

## Front-end surfaces

**MAUI** — `Views/BudgetPage.xaml` backed by `BudgetViewModel`: method picker (applies an
`AllocationRules` template), category list, transaction log, recurring bills, savings goals, and
the Pro-gated report section with its own CSV export (`BudgetReportCsvRenderer`).

**Blazor** — `Components/Pages/Budget.razor` (`/budget` route): the same feature set — income
setup with frequency-aware normalization, method picker, category bars, recurring bills, savings
goals, expense log, and the Pro-gated report with CSV and PDF export
(`Services/Export/BudgetReportCsvRenderer`, `BudgetReportPdfRenderer`).

Both view models/components construct the domain `Budget`/`BudgetTransaction`/`RecurringBill`/
`SavingsGoal` objects from their local UI state, call the shared `BudgetCalculator` /
`BudgetReportCalculator`, and map the DTOs back and forth for persistence — no budget math is
duplicated in either front-end.

---

## Tests

| Test file | Covers |
|---|---|
| `BudgetCalculatorTest` | Category summary, allocation types, projected month-end run-rate, recurring/goal totals |
| `BudgetReportCalculatorTest` | Multi-month aggregation, zero-spend months, variance sign |
| `RecurringBillTest` | Monthly-equivalent conversion across all frequencies |
| `SavingsGoalTest` | Contribution math, overdue/this-month collapse to full remaining, open-ended goals |
| `BudgetMergerTest` | Last-write-wins across all four collections, tie-break rules |

---

**Next:** [09 — Tax Data Files](09-tax-data-files.md)
