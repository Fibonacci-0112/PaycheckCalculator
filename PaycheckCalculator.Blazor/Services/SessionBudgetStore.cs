using PaycheckCalculator.Shared.Budgeting;

namespace PaycheckCalculator.Blazor.Services;

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
    private readonly Dictionary<Guid, RecurringBillDto> _bills = new();
    private readonly Dictionary<Guid, RecurringBillTombstone> _billTombs = new();
    private readonly Dictionary<Guid, SavingsGoalDto> _goals = new();
    private readonly Dictionary<Guid, SavingsGoalTombstone> _goalTombs = new();

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

    public Task<RecurringBillSet> LoadRecurringBillsAsync(CancellationToken ct = default)
        => Task.FromResult(new RecurringBillSet(_bills.Values.ToList(), _billTombs.Values.ToList()));

    public Task UpsertRecurringBillAsync(RecurringBillDto dto, CancellationToken ct = default)
    {
        _bills[dto.Id] = dto;
        _billTombs.Remove(dto.Id);
        return Task.CompletedTask;
    }

    public Task RemoveRecurringBillAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _bills.Remove(id);
        _billTombs[id] = new RecurringBillTombstone(id, deletedAtUtc);
        return Task.CompletedTask;
    }

    public Task ReplaceAllRecurringBillsAsync(RecurringBillSet set, CancellationToken ct = default)
    {
        _bills.Clear();
        _billTombs.Clear();
        foreach (var b in set.Bills)      _bills[b.Id]     = b;
        foreach (var t in set.Tombstones) _billTombs[t.Id] = t;
        return Task.CompletedTask;
    }

    public Task<SavingsGoalSet> LoadSavingsGoalsAsync(CancellationToken ct = default)
        => Task.FromResult(new SavingsGoalSet(_goals.Values.ToList(), _goalTombs.Values.ToList()));

    public Task UpsertSavingsGoalAsync(SavingsGoalDto dto, CancellationToken ct = default)
    {
        _goals[dto.Id] = dto;
        _goalTombs.Remove(dto.Id);
        return Task.CompletedTask;
    }

    public Task RemoveSavingsGoalAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _goals.Remove(id);
        _goalTombs[id] = new SavingsGoalTombstone(id, deletedAtUtc);
        return Task.CompletedTask;
    }

    public Task ReplaceAllSavingsGoalsAsync(SavingsGoalSet set, CancellationToken ct = default)
    {
        _goals.Clear();
        _goalTombs.Clear();
        foreach (var g in set.Goals)      _goals[g.Id]     = g;
        foreach (var t in set.Tombstones) _goalTombs[t.Id] = t;
        return Task.CompletedTask;
    }
}
