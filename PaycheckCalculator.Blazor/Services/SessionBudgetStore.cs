using System.Text.Json;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Json;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Budget + transaction store for the Blazor app. The authoritative copy lives in memory for the
/// lifetime of the Blazor Server circuit and is additionally mirrored into the browser's
/// <c>localStorage</c>, so anonymous (signed-out) budget data survives closing the tab — matching
/// the on-device persistence the MAUI app already provides. Signing in syncs it to the API server.
/// <para>
/// Persistence is best-effort: JS interop is unavailable during prerendering and
/// <c>localStorage</c> can be blocked outright, so the store keeps working in-memory either way.
/// Pages call <see cref="HydrateAsync"/> once interactive rendering has started.
/// </para>
/// </summary>
public sealed class SessionBudgetStore(BrowserLocalStorage storage) : IBudgetStore
{
    /// <summary>Browser storage key for the anonymous budget snapshot.</summary>
    internal const string StorageKey = "paycheckcalculator.budgets";

    private readonly BrowserLocalStorage _storage = storage;
    private readonly Dictionary<string, BudgetDto> _budgets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BudgetTombstone> _budgetTombs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, TransactionDto> _transactions = new();
    private readonly Dictionary<Guid, TransactionTombstone> _txTombs = new();
    private readonly Dictionary<Guid, RecurringBillDto> _bills = new();
    private readonly Dictionary<Guid, RecurringBillTombstone> _billTombs = new();
    private readonly Dictionary<Guid, SavingsGoalDto> _goals = new();
    private readonly Dictionary<Guid, SavingsGoalTombstone> _goalTombs = new();
    private bool _hydrated;

    /// <summary>
    /// Loads any previously persisted budget data from browser storage into this circuit. Safe to
    /// call repeatedly — only the first successful call populates the store, so a later hydration
    /// can never clobber data the user entered in this session. Returns true when data was restored.
    /// </summary>
    public async Task<bool> HydrateAsync(CancellationToken ct = default)
    {
        if (_hydrated) return false;

        var json = await _storage.GetAsync(StorageKey, ct).ConfigureAwait(false);
        if (!_storage.IsAvailable) return false;

        // Storage is reachable: mark hydrated even when empty so later writes are allowed through.
        _hydrated = true;
        if (string.IsNullOrWhiteSpace(json)) return false;

        StoreFile? file;
        try
        {
            file = JsonSerializer.Deserialize<StoreFile>(json, PaycheckJson.Options);
        }
        catch (JsonException)
        {
            // Corrupt or written by an incompatible build — drop it rather than fail the page.
            await _storage.RemoveAsync(StorageKey, ct).ConfigureAwait(false);
            return false;
        }

        if (file is null || file.SchemaVersion != StoreFile.CurrentSchemaVersion)
        {
            await _storage.RemoveAsync(StorageKey, ct).ConfigureAwait(false);
            return false;
        }

        foreach (var b in file.Budgets) _budgets[b.Name] = b;
        foreach (var t in file.BudgetTombstones) _budgetTombs[t.Name] = t;
        foreach (var t in file.Transactions) _transactions[t.Id] = t;
        foreach (var t in file.TransactionTombstones) _txTombs[t.Id] = t;
        foreach (var b in file.RecurringBills) _bills[b.Id] = b;
        foreach (var t in file.RecurringBillTombstones) _billTombs[t.Id] = t;
        foreach (var g in file.SavingsGoals) _goals[g.Id] = g;
        foreach (var t in file.SavingsGoalTombstones) _goalTombs[t.Id] = t;

        return _budgets.Count > 0 || _transactions.Count > 0 || _bills.Count > 0 || _goals.Count > 0;
    }

    public Task<BudgetSet> LoadBudgetsAsync(CancellationToken ct = default)
        => Task.FromResult(new BudgetSet(_budgets.Values.ToList(), _budgetTombs.Values.ToList()));

    public Task UpsertBudgetAsync(BudgetDto dto, CancellationToken ct = default)
    {
        _budgets[dto.Name] = dto;
        _budgetTombs.Remove(dto.Name);
        return PersistAsync(ct);
    }

    public Task RemoveBudgetAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _budgets.Remove(name);
        _budgetTombs[name] = new BudgetTombstone(name, deletedAtUtc);
        return PersistAsync(ct);
    }

    public Task ReplaceAllBudgetsAsync(BudgetSet set, CancellationToken ct = default)
    {
        _budgets.Clear();
        _budgetTombs.Clear();
        foreach (var b in set.Budgets)    _budgets[b.Name]     = b;
        foreach (var t in set.Tombstones) _budgetTombs[t.Name] = t;
        return PersistAsync(ct);
    }

    public Task<TransactionSet> LoadTransactionsAsync(CancellationToken ct = default)
        => Task.FromResult(new TransactionSet(_transactions.Values.ToList(), _txTombs.Values.ToList()));

    public Task UpsertTransactionAsync(TransactionDto dto, CancellationToken ct = default)
    {
        _transactions[dto.Id] = dto;
        _txTombs.Remove(dto.Id);
        return PersistAsync(ct);
    }

    public Task RemoveTransactionAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _transactions.Remove(id);
        _txTombs[id] = new TransactionTombstone(id, deletedAtUtc);
        return PersistAsync(ct);
    }

    public Task ReplaceAllTransactionsAsync(TransactionSet set, CancellationToken ct = default)
    {
        _transactions.Clear();
        _txTombs.Clear();
        foreach (var t in set.Transactions) _transactions[t.Id] = t;
        foreach (var t in set.Tombstones)   _txTombs[t.Id]      = t;
        return PersistAsync(ct);
    }

    public Task<RecurringBillSet> LoadRecurringBillsAsync(CancellationToken ct = default)
        => Task.FromResult(new RecurringBillSet(_bills.Values.ToList(), _billTombs.Values.ToList()));

    public Task UpsertRecurringBillAsync(RecurringBillDto dto, CancellationToken ct = default)
    {
        _bills[dto.Id] = dto;
        _billTombs.Remove(dto.Id);
        return PersistAsync(ct);
    }

    public Task RemoveRecurringBillAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _bills.Remove(id);
        _billTombs[id] = new RecurringBillTombstone(id, deletedAtUtc);
        return PersistAsync(ct);
    }

    public Task ReplaceAllRecurringBillsAsync(RecurringBillSet set, CancellationToken ct = default)
    {
        _bills.Clear();
        _billTombs.Clear();
        foreach (var b in set.Bills)      _bills[b.Id]     = b;
        foreach (var t in set.Tombstones) _billTombs[t.Id] = t;
        return PersistAsync(ct);
    }

    public Task<SavingsGoalSet> LoadSavingsGoalsAsync(CancellationToken ct = default)
        => Task.FromResult(new SavingsGoalSet(_goals.Values.ToList(), _goalTombs.Values.ToList()));

    public Task UpsertSavingsGoalAsync(SavingsGoalDto dto, CancellationToken ct = default)
    {
        _goals[dto.Id] = dto;
        _goalTombs.Remove(dto.Id);
        return PersistAsync(ct);
    }

    public Task RemoveSavingsGoalAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _goals.Remove(id);
        _goalTombs[id] = new SavingsGoalTombstone(id, deletedAtUtc);
        return PersistAsync(ct);
    }

    public Task ReplaceAllSavingsGoalsAsync(SavingsGoalSet set, CancellationToken ct = default)
    {
        _goals.Clear();
        _goalTombs.Clear();
        foreach (var g in set.Goals)      _goals[g.Id]     = g;
        foreach (var t in set.Tombstones) _goalTombs[t.Id] = t;
        return PersistAsync(ct);
    }

    /// <summary>
    /// Removes the persisted copy entirely (used when the user deletes their local data). The
    /// in-memory state is untouched — callers clear that through the normal store operations.
    /// </summary>
    public Task ForgetPersistedAsync(CancellationToken ct = default)
        => _storage.RemoveAsync(StorageKey, ct);

    private async Task PersistAsync(CancellationToken ct)
    {
        // Never write before a successful hydration: doing so during prerender (or before the
        // stored snapshot has been read back) would overwrite the user's data with an empty set.
        if (!_hydrated) return;

        var file = new StoreFile
        {
            Budgets = _budgets.Values.ToList(),
            BudgetTombstones = _budgetTombs.Values.ToList(),
            Transactions = _transactions.Values.ToList(),
            TransactionTombstones = _txTombs.Values.ToList(),
            RecurringBills = _bills.Values.ToList(),
            RecurringBillTombstones = _billTombs.Values.ToList(),
            SavingsGoals = _goals.Values.ToList(),
            SavingsGoalTombstones = _goalTombs.Values.ToList()
        };

        await _storage.SetAsync(StorageKey, JsonSerializer.Serialize(file, PaycheckJson.Options), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Versioned envelope, mirroring the MAUI <c>JsonFileBudgetStore</c> file format so both clients
    /// persist the same shape.
    /// </summary>
    private sealed class StoreFile
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<BudgetDto> Budgets { get; set; } = [];
        public List<BudgetTombstone> BudgetTombstones { get; set; } = [];
        public List<TransactionDto> Transactions { get; set; } = [];
        public List<TransactionTombstone> TransactionTombstones { get; set; } = [];
        public List<RecurringBillDto> RecurringBills { get; set; } = [];
        public List<RecurringBillTombstone> RecurringBillTombstones { get; set; } = [];
        public List<SavingsGoalDto> SavingsGoals { get; set; } = [];
        public List<SavingsGoalTombstone> SavingsGoalTombstones { get; set; } = [];
    }
}
