namespace PaycheckCalc.Shared.Budgeting;

/// <summary>A client's full budget state pushed to the server for merging.</summary>
public sealed record BudgetSyncRequest(
    IReadOnlyList<BudgetDto> Budgets,
    IReadOnlyList<BudgetTombstone> BudgetTombstones,
    IReadOnlyList<TransactionDto> Transactions,
    IReadOnlyList<TransactionTombstone> TransactionTombstones,
    IReadOnlyList<RecurringBillDto> RecurringBills,
    IReadOnlyList<RecurringBillTombstone> RecurringBillTombstones,
    IReadOnlyList<SavingsGoalDto> SavingsGoals,
    IReadOnlyList<SavingsGoalTombstone> SavingsGoalTombstones)
{
    public static BudgetSyncRequest From(
        BudgetSet budgets,
        TransactionSet transactions,
        RecurringBillSet bills,
        SavingsGoalSet goals) =>
        new(budgets.Budgets, budgets.Tombstones,
            transactions.Transactions, transactions.Tombstones,
            bills.Bills, bills.Tombstones,
            goals.Goals, goals.Tombstones);
}

/// <summary>The merged state the server returns; the client replaces its local state with this.</summary>
public sealed record BudgetSyncResponse(
    IReadOnlyList<BudgetDto> Budgets,
    IReadOnlyList<BudgetTombstone> BudgetTombstones,
    IReadOnlyList<TransactionDto> Transactions,
    IReadOnlyList<TransactionTombstone> TransactionTombstones,
    IReadOnlyList<RecurringBillDto> RecurringBills,
    IReadOnlyList<RecurringBillTombstone> RecurringBillTombstones,
    IReadOnlyList<SavingsGoalDto> SavingsGoals,
    IReadOnlyList<SavingsGoalTombstone> SavingsGoalTombstones,
    DateTimeOffset ServerTimeUtc)
{
    public (BudgetSet Budgets, TransactionSet Transactions, RecurringBillSet Bills, SavingsGoalSet Goals) ToSets() =>
        (new BudgetSet(Budgets, BudgetTombstones),
         new TransactionSet(Transactions, TransactionTombstones),
         new RecurringBillSet(RecurringBills, RecurringBillTombstones),
         new SavingsGoalSet(SavingsGoals, SavingsGoalTombstones));
}
