namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// Where budgets and transactions live for one client. MAUI backs this with a JSON file in app
/// data; Blazor backs it with circuit memory. The sync orchestration is identical regardless of
/// the backing store.
/// </summary>
public interface IBudgetStore
{
    // ── Budgets ──────────────────────────────────────────────────────────────
    Task<BudgetSet> LoadBudgetsAsync(CancellationToken ct = default);
    Task UpsertBudgetAsync(BudgetDto dto, CancellationToken ct = default);
    Task RemoveBudgetAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ReplaceAllBudgetsAsync(BudgetSet set, CancellationToken ct = default);

    // ── Transactions ─────────────────────────────────────────────────────────
    Task<TransactionSet> LoadTransactionsAsync(CancellationToken ct = default);
    Task UpsertTransactionAsync(TransactionDto dto, CancellationToken ct = default);
    Task RemoveTransactionAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ReplaceAllTransactionsAsync(TransactionSet set, CancellationToken ct = default);

    // ── Recurring bills ────────────────────────────────────────────────────────
    Task<RecurringBillSet> LoadRecurringBillsAsync(CancellationToken ct = default);
    Task UpsertRecurringBillAsync(RecurringBillDto dto, CancellationToken ct = default);
    Task RemoveRecurringBillAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ReplaceAllRecurringBillsAsync(RecurringBillSet set, CancellationToken ct = default);

    // ── Savings goals ──────────────────────────────────────────────────────────
    Task<SavingsGoalSet> LoadSavingsGoalsAsync(CancellationToken ct = default);
    Task UpsertSavingsGoalAsync(SavingsGoalDto dto, CancellationToken ct = default);
    Task RemoveSavingsGoalAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default);
    Task ReplaceAllSavingsGoalsAsync(SavingsGoalSet set, CancellationToken ct = default);
}
