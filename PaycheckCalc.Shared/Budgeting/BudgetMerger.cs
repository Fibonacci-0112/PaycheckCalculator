namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// Deterministic last-write-wins merge for budgets (keyed by name, case-insensitive) and
/// transactions (keyed by stable GUID). Same three-level tie-breaking rules as
/// <c>SavedPaycheckMerger</c>: later timestamp wins; on a tie, live beats tombstone; same-kind
/// tie goes to the incoming side (idempotent re-sync).
/// </summary>
public static class BudgetMerger
{
    public static BudgetSet MergeBudgets(BudgetSet existing, BudgetSet incoming)
    {
        var winners = new Dictionary<string, BudgetCandidate>(StringComparer.OrdinalIgnoreCase);

        void Consider(BudgetCandidate c)
        {
            if (!winners.TryGetValue(c.Key, out var cur) || c.Beats(cur))
                winners[c.Key] = c;
        }

        foreach (var e in existing.Budgets)   Consider(BudgetCandidate.ForEntry(e,    fromIncoming: false));
        foreach (var t in existing.Tombstones) Consider(BudgetCandidate.ForTombstone(t, fromIncoming: false));
        foreach (var e in incoming.Budgets)   Consider(BudgetCandidate.ForEntry(e,    fromIncoming: true));
        foreach (var t in incoming.Tombstones) Consider(BudgetCandidate.ForTombstone(t, fromIncoming: true));

        var budgets    = new List<BudgetDto>();
        var tombstones = new List<BudgetTombstone>();
        foreach (var w in winners.Values)
        {
            if (w.Entry is not null) budgets.Add(w.Entry);
            else tombstones.Add(w.Tombstone!);
        }

        budgets.Sort(static (a, b)    => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        tombstones.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new BudgetSet(budgets, tombstones);
    }

    public static TransactionSet MergeTransactions(TransactionSet existing, TransactionSet incoming)
    {
        var winners = new Dictionary<Guid, TxCandidate>();

        void Consider(TxCandidate c)
        {
            if (!winners.TryGetValue(c.Id, out var cur) || c.Beats(cur))
                winners[c.Id] = c;
        }

        foreach (var e in existing.Transactions) Consider(TxCandidate.ForEntry(e,    fromIncoming: false));
        foreach (var t in existing.Tombstones)   Consider(TxCandidate.ForTombstone(t, fromIncoming: false));
        foreach (var e in incoming.Transactions) Consider(TxCandidate.ForEntry(e,    fromIncoming: true));
        foreach (var t in incoming.Tombstones)   Consider(TxCandidate.ForTombstone(t, fromIncoming: true));

        var transactions = new List<TransactionDto>();
        var tombstones   = new List<TransactionTombstone>();
        foreach (var w in winners.Values)
        {
            if (w.Entry is not null) transactions.Add(w.Entry);
            else tombstones.Add(w.Tombstone!);
        }

        transactions.Sort(static (a, b) =>
        {
            var d = a.Date.CompareTo(b.Date);
            return d != 0 ? d : a.Id.CompareTo(b.Id);
        });
        tombstones.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return new TransactionSet(transactions, tombstones);
    }

    public static RecurringBillSet MergeRecurringBills(RecurringBillSet existing, RecurringBillSet incoming)
    {
        var winners = new Dictionary<Guid, BillCandidate>();

        void Consider(BillCandidate c)
        {
            if (!winners.TryGetValue(c.Id, out var cur) || c.Beats(cur))
                winners[c.Id] = c;
        }

        foreach (var e in existing.Bills)      Consider(BillCandidate.ForEntry(e,    fromIncoming: false));
        foreach (var t in existing.Tombstones) Consider(BillCandidate.ForTombstone(t, fromIncoming: false));
        foreach (var e in incoming.Bills)      Consider(BillCandidate.ForEntry(e,    fromIncoming: true));
        foreach (var t in incoming.Tombstones) Consider(BillCandidate.ForTombstone(t, fromIncoming: true));

        var bills      = new List<RecurringBillDto>();
        var tombstones = new List<RecurringBillTombstone>();
        foreach (var w in winners.Values)
        {
            if (w.Entry is not null) bills.Add(w.Entry);
            else tombstones.Add(w.Tombstone!);
        }

        bills.Sort(static (a, b) =>
        {
            var d = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return d != 0 ? d : a.Id.CompareTo(b.Id);
        });
        tombstones.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return new RecurringBillSet(bills, tombstones);
    }

    public static SavingsGoalSet MergeSavingsGoals(SavingsGoalSet existing, SavingsGoalSet incoming)
    {
        var winners = new Dictionary<Guid, GoalCandidate>();

        void Consider(GoalCandidate c)
        {
            if (!winners.TryGetValue(c.Id, out var cur) || c.Beats(cur))
                winners[c.Id] = c;
        }

        foreach (var e in existing.Goals)      Consider(GoalCandidate.ForEntry(e,    fromIncoming: false));
        foreach (var t in existing.Tombstones) Consider(GoalCandidate.ForTombstone(t, fromIncoming: false));
        foreach (var e in incoming.Goals)      Consider(GoalCandidate.ForEntry(e,    fromIncoming: true));
        foreach (var t in incoming.Tombstones) Consider(GoalCandidate.ForTombstone(t, fromIncoming: true));

        var goals      = new List<SavingsGoalDto>();
        var tombstones = new List<SavingsGoalTombstone>();
        foreach (var w in winners.Values)
        {
            if (w.Entry is not null) goals.Add(w.Entry);
            else tombstones.Add(w.Tombstone!);
        }

        goals.Sort(static (a, b) =>
        {
            var d = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return d != 0 ? d : a.Id.CompareTo(b.Id);
        });
        tombstones.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return new SavingsGoalSet(goals, tombstones);
    }

    private sealed class BudgetCandidate
    {
        public required string Key { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public bool IsTombstone { get; init; }
        public bool FromIncoming { get; init; }
        public BudgetDto? Entry { get; init; }
        public BudgetTombstone? Tombstone { get; init; }

        public static BudgetCandidate ForEntry(BudgetDto dto, bool fromIncoming) => new()
        { Key = dto.Name, Timestamp = dto.UpdatedAtUtc, IsTombstone = false, FromIncoming = fromIncoming, Entry = dto };

        public static BudgetCandidate ForTombstone(BudgetTombstone t, bool fromIncoming) => new()
        { Key = t.Name, Timestamp = t.DeletedAtUtc, IsTombstone = true, FromIncoming = fromIncoming, Tombstone = t };

        public bool Beats(BudgetCandidate other)
        {
            if (Timestamp != other.Timestamp) return Timestamp > other.Timestamp;
            if (IsTombstone != other.IsTombstone) return !IsTombstone;
            return FromIncoming && !other.FromIncoming;
        }
    }

    private sealed class TxCandidate
    {
        public Guid Id { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public bool IsTombstone { get; init; }
        public bool FromIncoming { get; init; }
        public TransactionDto? Entry { get; init; }
        public TransactionTombstone? Tombstone { get; init; }

        public static TxCandidate ForEntry(TransactionDto dto, bool fromIncoming) => new()
        { Id = dto.Id, Timestamp = dto.UpdatedAtUtc, IsTombstone = false, FromIncoming = fromIncoming, Entry = dto };

        public static TxCandidate ForTombstone(TransactionTombstone t, bool fromIncoming) => new()
        { Id = t.Id, Timestamp = t.DeletedAtUtc, IsTombstone = true, FromIncoming = fromIncoming, Tombstone = t };

        public bool Beats(TxCandidate other)
        {
            if (Timestamp != other.Timestamp) return Timestamp > other.Timestamp;
            if (IsTombstone != other.IsTombstone) return !IsTombstone;
            return FromIncoming && !other.FromIncoming;
        }
    }

    private sealed class BillCandidate
    {
        public Guid Id { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public bool IsTombstone { get; init; }
        public bool FromIncoming { get; init; }
        public RecurringBillDto? Entry { get; init; }
        public RecurringBillTombstone? Tombstone { get; init; }

        public static BillCandidate ForEntry(RecurringBillDto dto, bool fromIncoming) => new()
        { Id = dto.Id, Timestamp = dto.UpdatedAtUtc, IsTombstone = false, FromIncoming = fromIncoming, Entry = dto };

        public static BillCandidate ForTombstone(RecurringBillTombstone t, bool fromIncoming) => new()
        { Id = t.Id, Timestamp = t.DeletedAtUtc, IsTombstone = true, FromIncoming = fromIncoming, Tombstone = t };

        public bool Beats(BillCandidate other)
        {
            if (Timestamp != other.Timestamp) return Timestamp > other.Timestamp;
            if (IsTombstone != other.IsTombstone) return !IsTombstone;
            return FromIncoming && !other.FromIncoming;
        }
    }

    private sealed class GoalCandidate
    {
        public Guid Id { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public bool IsTombstone { get; init; }
        public bool FromIncoming { get; init; }
        public SavingsGoalDto? Entry { get; init; }
        public SavingsGoalTombstone? Tombstone { get; init; }

        public static GoalCandidate ForEntry(SavingsGoalDto dto, bool fromIncoming) => new()
        { Id = dto.Id, Timestamp = dto.UpdatedAtUtc, IsTombstone = false, FromIncoming = fromIncoming, Entry = dto };

        public static GoalCandidate ForTombstone(SavingsGoalTombstone t, bool fromIncoming) => new()
        { Id = t.Id, Timestamp = t.DeletedAtUtc, IsTombstone = true, FromIncoming = fromIncoming, Tombstone = t };

        public bool Beats(GoalCandidate other)
        {
            if (Timestamp != other.Timestamp) return Timestamp > other.Timestamp;
            if (IsTombstone != other.IsTombstone) return !IsTombstone;
            return FromIncoming && !other.FromIncoming;
        }
    }
}
