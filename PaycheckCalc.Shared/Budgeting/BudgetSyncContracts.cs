namespace PaycheckCalc.Shared.Budgeting;

/// <summary>A client's full budget + transaction state pushed to the server for merging.</summary>
public sealed record BudgetSyncRequest(
    IReadOnlyList<BudgetDto> Budgets,
    IReadOnlyList<BudgetTombstone> BudgetTombstones,
    IReadOnlyList<TransactionDto> Transactions,
    IReadOnlyList<TransactionTombstone> TransactionTombstones)
{
    public static BudgetSyncRequest From(BudgetSet budgets, TransactionSet transactions) =>
        new(budgets.Budgets, budgets.Tombstones, transactions.Transactions, transactions.Tombstones);
}

/// <summary>The merged state the server returns; the client replaces its local state with this.</summary>
public sealed record BudgetSyncResponse(
    IReadOnlyList<BudgetDto> Budgets,
    IReadOnlyList<BudgetTombstone> BudgetTombstones,
    IReadOnlyList<TransactionDto> Transactions,
    IReadOnlyList<TransactionTombstone> TransactionTombstones,
    DateTimeOffset ServerTimeUtc)
{
    public (BudgetSet Budgets, TransactionSet Transactions) ToSets() =>
        (new BudgetSet(Budgets, BudgetTombstones), new TransactionSet(Transactions, TransactionTombstones));
}
