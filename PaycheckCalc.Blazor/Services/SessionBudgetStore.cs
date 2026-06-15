using PaycheckCalc.Shared.Budgeting;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// In-memory budget + transaction store for the Blazor app. Registered as scoped (circuit-lifetime),
/// so anonymous data is discarded when the browser tab closes. Signing in syncs session data to the
/// API server, which retains it across sessions.
/// </summary>
public sealed class SessionBudgetStore : IBudgetStore
{
    private readonly Dictionary<string, BudgetDto> _budgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BudgetTombstone> _budgetTombs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, TransactionDto> _transactions = new();
    private readonly Dictionary<Guid, TransactionTombstone> _txTombs = new();

    public Task<BudgetSet> LoadBudgetsAsync(CancellationToken ct = default)
        => Task.FromResult(new BudgetSet(_budgets.Values.ToList(), _budgetTombs.Values.ToList()));

    public Task UpsertBudgetAsync(BudgetDto dto, CancellationToken ct = default)
    {
        _budgets[dto.Name] = dto;
        _budgetTombs.Remove(dto.Name);
        return Task.CompletedTask;
    }

    public Task RemoveBudgetAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _budgets.Remove(name);
        _budgetTombs[name] = new BudgetTombstone(name, deletedAtUtc);
        return Task.CompletedTask;
    }

    public Task ReplaceAllBudgetsAsync(BudgetSet set, CancellationToken ct = default)
    {
        _budgets.Clear();
        _budgetTombs.Clear();
        foreach (var b in set.Budgets)    _budgets[b.Name]     = b;
        foreach (var t in set.Tombstones) _budgetTombs[t.Name] = t;
        return Task.CompletedTask;
    }

    public Task<TransactionSet> LoadTransactionsAsync(CancellationToken ct = default)
        => Task.FromResult(new TransactionSet(_transactions.Values.ToList(), _txTombs.Values.ToList()));

    public Task UpsertTransactionAsync(TransactionDto dto, CancellationToken ct = default)
    {
        _transactions[dto.Id] = dto;
        _txTombs.Remove(dto.Id);
        return Task.CompletedTask;
    }

    public Task RemoveTransactionAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _transactions.Remove(id);
        _txTombs[id] = new TransactionTombstone(id, deletedAtUtc);
        return Task.CompletedTask;
    }

    public Task ReplaceAllTransactionsAsync(TransactionSet set, CancellationToken ct = default)
    {
        _transactions.Clear();
        _txTombs.Clear();
        foreach (var t in set.Transactions) _transactions[t.Id] = t;
        foreach (var t in set.Tombstones)   _txTombs[t.Id]      = t;
        return Task.CompletedTask;
    }
}
