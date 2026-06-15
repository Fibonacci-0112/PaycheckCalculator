using PaycheckCalc.Core.Budgeting;
using PaycheckCalc.Shared.Budgeting;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="BudgetMerger"/> — the last-write-wins merge for budgets (name-keyed)
/// and transactions (GUID-keyed). Mirrors the structure of <c>SavedPaycheckMergerTest</c>.
/// </summary>
public sealed class BudgetMergerTest
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int months) => Base.AddMonths(months);

    // ── Budget merge ──────────────────────────────────────────────────────────

    [Fact]
    public void MergeBudgets_NewerIncoming_WinsOverOlderExisting()
    {
        var existing = BudgetSet(Budget("Work Budget", At(1), 3_000m));
        var incoming = BudgetSet(Budget("Work Budget", At(2), 4_000m)); // newer

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Single(merged.Budgets);
        Assert.Equal(4_000m, merged.Budgets[0].MonthlyNetIncome);
    }

    [Fact]
    public void MergeBudgets_NewerExisting_BeatsIncoming()
    {
        var existing = BudgetSet(Budget("Work Budget", At(3), 5_000m)); // newer
        var incoming = BudgetSet(Budget("Work Budget", At(1), 2_000m));

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Single(merged.Budgets);
        Assert.Equal(5_000m, merged.Budgets[0].MonthlyNetIncome);
    }

    [Fact]
    public void MergeBudgets_Tombstone_ErasesOlderEntry()
    {
        var existing = BudgetSet(Budget("Work Budget", At(1), 3_000m));
        var incoming = new Shared.Budgeting.BudgetSet([], [new BudgetTombstone("Work Budget", At(2))]);

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Empty(merged.Budgets);
        Assert.Single(merged.Tombstones);
    }

    [Fact]
    public void MergeBudgets_NewerEntry_BeatsOlderTombstone()
    {
        var existing = new Shared.Budgeting.BudgetSet([], [new BudgetTombstone("Work Budget", At(1))]);
        var incoming = BudgetSet(Budget("Work Budget", At(2), 4_000m)); // newer

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Single(merged.Budgets);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void MergeBudgets_CaseInsensitiveNameKey()
    {
        var existing = BudgetSet(Budget("Work Budget", At(1), 3_000m));
        var incoming = BudgetSet(Budget("WORK BUDGET", At(2), 4_000m)); // same budget, different casing

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Single(merged.Budgets);
        Assert.Equal(4_000m, merged.Budgets[0].MonthlyNetIncome);
    }

    [Fact]
    public void MergeBudgets_MultipleBudgets_SortedByName()
    {
        var existing = BudgetSet(Budget("Zeta", At(1), 1_000m));
        var incoming = BudgetSet(Budget("Alpha", At(1), 2_000m));

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Equal(2, merged.Budgets.Count);
        Assert.Equal("Alpha", merged.Budgets[0].Name);
        Assert.Equal("Zeta",  merged.Budgets[1].Name);
    }

    [Fact]
    public void MergeBudgets_OnTimestampTie_LiveBeatsGrave()
    {
        var ts = At(1);
        var existing = new Shared.Budgeting.BudgetSet([], [new BudgetTombstone("X", ts)]);
        var incoming = BudgetSet(Budget("X", ts, 1_000m));

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Single(merged.Budgets);  // live entry wins the tie
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void MergeBudgets_OnExactTieBetweenLiveEntries_IncomingWins()
    {
        var ts = At(1);
        var existing = BudgetSet(Budget("X", ts, 1_000m));
        var incoming = BudgetSet(Budget("X", ts, 9_000m));

        var merged = BudgetMerger.MergeBudgets(existing, incoming);

        Assert.Equal(9_000m, merged.Budgets[0].MonthlyNetIncome); // incoming wins
    }

    // ── Transaction merge ──────────────────────────────────────────────────────

    [Fact]
    public void MergeTransactions_NewerIncoming_WinsOverExisting()
    {
        var id = Guid.NewGuid();
        var existing = TxSet(Tx(id, 50m, new DateOnly(2026, 6, 5), At(1)));
        var incoming = TxSet(Tx(id, 75m, new DateOnly(2026, 6, 5), At(2))); // newer

        var merged = BudgetMerger.MergeTransactions(existing, incoming);

        Assert.Single(merged.Transactions);
        Assert.Equal(75m, merged.Transactions[0].Amount);
    }

    [Fact]
    public void MergeTransactions_Tombstone_ErasesOlderTransaction()
    {
        var id = Guid.NewGuid();
        var existing = TxSet(Tx(id, 50m, new DateOnly(2026, 6, 5), At(1)));
        var incoming  = new TransactionSet([], [new TransactionTombstone(id, At(2))]);

        var merged = BudgetMerger.MergeTransactions(existing, incoming);

        Assert.Empty(merged.Transactions);
        Assert.Single(merged.Tombstones);
    }

    [Fact]
    public void MergeTransactions_NewerEntry_BeatsOlderTombstone()
    {
        var id = Guid.NewGuid();
        var existing = new TransactionSet([], [new TransactionTombstone(id, At(1))]);
        var incoming  = TxSet(Tx(id, 80m, new DateOnly(2026, 6, 5), At(2))); // newer

        var merged = BudgetMerger.MergeTransactions(existing, incoming);

        Assert.Single(merged.Transactions);
        Assert.Equal(80m, merged.Transactions[0].Amount);
    }

    [Fact]
    public void MergeTransactions_MultipleTransactions_SortedByDate()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var existing = TxSet(Tx(id1, 100m, new DateOnly(2026, 6, 20), At(1)));
        var incoming  = TxSet(Tx(id2, 200m, new DateOnly(2026, 6, 5),  At(1)));

        var merged = BudgetMerger.MergeTransactions(existing, incoming);

        Assert.Equal(2, merged.Transactions.Count);
        Assert.Equal(new DateOnly(2026, 6, 5),  merged.Transactions[0].Date); // earlier date first
        Assert.Equal(new DateOnly(2026, 6, 20), merged.Transactions[1].Date);
    }

    [Fact]
    public void MergeTransactions_IndependentIds_BothPreserved()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var existing = TxSet(Tx(id1, 50m, new DateOnly(2026, 6, 1), At(1)));
        var incoming  = TxSet(Tx(id2, 75m, new DateOnly(2026, 6, 2), At(1)));

        var merged = BudgetMerger.MergeTransactions(existing, incoming);

        Assert.Equal(2, merged.Transactions.Count);
    }

    // ── Recurring bill merge ────────────────────────────────────────────────────

    [Fact]
    public void MergeRecurringBills_NewerIncoming_WinsOverExisting()
    {
        var id = Guid.NewGuid();
        var existing = BillSet(Bill(id, "Rent", 1_500m, At(1)));
        var incoming = BillSet(Bill(id, "Rent", 1_600m, At(2))); // newer

        var merged = BudgetMerger.MergeRecurringBills(existing, incoming);

        Assert.Single(merged.Bills);
        Assert.Equal(1_600m, merged.Bills[0].Amount);
    }

    [Fact]
    public void MergeRecurringBills_Tombstone_ErasesOlderBill()
    {
        var id = Guid.NewGuid();
        var existing = BillSet(Bill(id, "Rent", 1_500m, At(1)));
        var incoming = new RecurringBillSet([], [new RecurringBillTombstone(id, At(2))]);

        var merged = BudgetMerger.MergeRecurringBills(existing, incoming);

        Assert.Empty(merged.Bills);
        Assert.Single(merged.Tombstones);
    }

    [Fact]
    public void MergeRecurringBills_MultipleBills_SortedByName()
    {
        var existing = BillSet(Bill(Guid.NewGuid(), "Water", 40m, At(1)));
        var incoming = BillSet(Bill(Guid.NewGuid(), "Electric", 90m, At(1)));

        var merged = BudgetMerger.MergeRecurringBills(existing, incoming);

        Assert.Equal(2, merged.Bills.Count);
        Assert.Equal("Electric", merged.Bills[0].Name);
        Assert.Equal("Water",    merged.Bills[1].Name);
    }

    // ── Savings goal merge ───────────────────────────────────────────────────────

    [Fact]
    public void MergeSavingsGoals_NewerIncoming_WinsOverExisting()
    {
        var id = Guid.NewGuid();
        var existing = GoalSet(Goal(id, "Car", 10_000m, 1_000m, At(1)));
        var incoming = GoalSet(Goal(id, "Car", 10_000m, 2_500m, At(2))); // newer

        var merged = BudgetMerger.MergeSavingsGoals(existing, incoming);

        Assert.Single(merged.Goals);
        Assert.Equal(2_500m, merged.Goals[0].CurrentAmount);
    }

    [Fact]
    public void MergeSavingsGoals_NewerEntry_BeatsOlderTombstone()
    {
        var id = Guid.NewGuid();
        var existing = new SavingsGoalSet([], [new SavingsGoalTombstone(id, At(1))]);
        var incoming = GoalSet(Goal(id, "Car", 10_000m, 500m, At(2))); // newer

        var merged = BudgetMerger.MergeSavingsGoals(existing, incoming);

        Assert.Single(merged.Goals);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void MergeSavingsGoals_IndependentIds_BothPreserved()
    {
        var existing = GoalSet(Goal(Guid.NewGuid(), "Car", 10_000m, 0m, At(1)));
        var incoming = GoalSet(Goal(Guid.NewGuid(), "House", 50_000m, 0m, At(1)));

        var merged = BudgetMerger.MergeSavingsGoals(existing, incoming);

        Assert.Equal(2, merged.Goals.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BudgetDto Budget(string name, DateTimeOffset updatedAt, decimal income) => new()
    {
        Name = name,
        UpdatedAtUtc = updatedAt,
        MonthlyNetIncome = income,
        Categories = []
    };

    private static Shared.Budgeting.BudgetSet BudgetSet(BudgetDto dto) =>
        new([dto], []);

    private static TransactionDto Tx(Guid id, decimal amount, DateOnly date, DateTimeOffset updatedAt) => new()
    {
        Id = id,
        BudgetName = "Test",
        CategoryName = "Needs",
        Amount = amount,
        Date = date,
        UpdatedAtUtc = updatedAt
    };

    private static TransactionSet TxSet(TransactionDto dto) => new([dto], []);

    private static RecurringBillDto Bill(Guid id, string name, decimal amount, DateTimeOffset updatedAt) => new()
    {
        Id = id,
        BudgetName = "Test",
        Name = name,
        CategoryName = "Needs",
        Amount = amount,
        Frequency = RecurrenceFrequency.Monthly,
        UpdatedAtUtc = updatedAt
    };

    private static RecurringBillSet BillSet(RecurringBillDto dto) => new([dto], []);

    private static SavingsGoalDto Goal(Guid id, string name, decimal target, decimal current, DateTimeOffset updatedAt) => new()
    {
        Id = id,
        BudgetName = "Test",
        Name = name,
        TargetAmount = target,
        CurrentAmount = current,
        UpdatedAtUtc = updatedAt
    };

    private static SavingsGoalSet GoalSet(SavingsGoalDto dto) => new([dto], []);
}
