using PaycheckCalc.Shared.Client;

namespace PaycheckCalc.Shared.Budgeting;

/// <summary>Result of a budget sync attempt: merged sets on success, or an error message.</summary>
public sealed record BudgetSyncOutcome(
    bool Success,
    BudgetSet? MergedBudgets,
    TransactionSet? MergedTransactions,
    string? Error)
{
    public static BudgetSyncOutcome Ok(BudgetSet budgets, TransactionSet transactions) =>
        new(true, budgets, transactions, null);
    public static BudgetSyncOutcome Fail(string error) => new(false, null, null, error);
}

/// <summary>
/// Orchestrates a one-shot budget sync: load the local store, push to the API, replace local
/// state with the server-merged result. The merge itself runs server-side (see
/// <see cref="BudgetMerger"/>), so clients never need their own conflict resolution.
/// </summary>
public sealed class BudgetSyncService
{
    private readonly IBudgetStore _store;
    private readonly PaycheckApiClient _api;

    public BudgetSyncService(IBudgetStore store, PaycheckApiClient api)
    {
        _store = store;
        _api = api;
    }

    public async Task<BudgetSyncOutcome> SyncAsync(CancellationToken ct = default)
    {
        var budgets      = await _store.LoadBudgetsAsync(ct).ConfigureAwait(false);
        var transactions = await _store.LoadTransactionsAsync(ct).ConfigureAwait(false);
        var request      = BudgetSyncRequest.From(budgets, transactions);

        var result = await _api.SyncBudgetsAsync(request, ct).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            return BudgetSyncOutcome.Fail(result.Error ?? "Sync failed.");

        var (mergedBudgets, mergedTransactions) = result.Value.ToSets();
        await _store.ReplaceAllBudgetsAsync(mergedBudgets, ct).ConfigureAwait(false);
        await _store.ReplaceAllTransactionsAsync(mergedTransactions, ct).ConfigureAwait(false);
        return BudgetSyncOutcome.Ok(mergedBudgets, mergedTransactions);
    }
}
