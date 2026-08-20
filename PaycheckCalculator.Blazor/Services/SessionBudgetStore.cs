using System.Text.Json;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Json;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Budget / transaction / recurring-bill / savings-goal store for the Blazor app. Registered as
/// scoped, so the in-memory working set lives for the Blazor Server circuit, and each mutation is
/// mirrored to the browser's <c>localStorage</c> so anonymous budgets survive a refresh, a closed
/// tab, and a new circuit — the web equivalent of the MAUI app's <c>JsonFileBudgetStore</c>. All
/// four domains share one envelope under a single key, matching that file's shape. Signing in
/// syncs the same data to the API server.
///
/// Persistence is strictly best-effort (see <see cref="BrowserLocalStorage"/>): during
/// prerendering, on a disconnected circuit, or with storage blocked, this behaves exactly like
/// the pure in-memory store it replaced.
/// </summary>
public sealed class SessionBudgetStore(BrowserLocalStorage storage) : IBudgetStore
{
    /// <summary>localStorage key. The trailing version is the envelope's, not a DTO's.</summary>
    internal const string StorageKey = "paycheckcalc.budgets.v1";

    /// <summary>
    /// Highest envelope <see cref="StoreFile.SchemaVersion"/> this build understands — 2 to match
    /// <see cref="BudgetDto.SchemaVersion"/> (C2 added <c>Method</c>). A payload written by a newer
    /// build is left untouched rather than parsed or overwritten.
    /// </summary>
    private const int CurrentSchemaVersion = 2;

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
    /// Loads persisted data into the in-memory sets, at most once per circuit. Safe to call from
    /// anywhere: while the browser is unreachable it is a no-op and a later call retries, which is
    /// what lets the first (prerendered) render fall back to empty sets and the first interactive
    /// render pick the real data up.
    /// </summary>
    public async Task EnsureHydratedAsync(CancellationToken ct = default)
    {
        if (_hydrated) return;

        var read = await _storage.GetAsync(StorageKey, ct).ConfigureAwait(false);
        if (!read.Available) return;

        _hydrated = true;
        if (string.IsNullOrWhiteSpace(read.Value)) return;

        StoreFile? file;
        try
        {
            file = JsonSerializer.Deserialize<StoreFile>(read.Value, PaycheckJson.Options);
        }
        catch (JsonException)
        {
            // Corrupt payload: drop it rather than failing every load for the rest of the session.
            await _storage.RemoveAsync(StorageKey, ct).ConfigureAwait(false);
            return;
        }

        if (file is null || file.SchemaVersion > CurrentSchemaVersion) return;

        // Anything already in memory was written this circuit and is newer, so it wins.
        Hydrate(file.Budgets, _budgets, _budgetTombs, b => b.Name);
        Hydrate(file.BudgetTombstones, _budgetTombs, _budgets, t => t.Name);
        Hydrate(file.Transactions, _transactions, _txTombs, t => t.Id);
        Hydrate(file.TransactionTombstones, _txTombs, _transactions, t => t.Id);
        Hydrate(file.RecurringBills, _bills, _billTombs, b => b.Id);
        Hydrate(file.RecurringBillTombstones, _billTombs, _bills, t => t.Id);
        Hydrate(file.SavingsGoals, _goals, _goalTombs, g => g.Id);
        Hydrate(file.SavingsGoalTombstones, _goalTombs, _goals, t => t.Id);
    }

    /// <summary>
    /// Adds each stored item to <paramref name="target"/> under its key, unless that key is
    /// already claimed in memory — by <paramref name="target"/> itself or by its
    /// <paramref name="opposite"/> live/tombstone counterpart.
    /// </summary>
    private static void Hydrate<TKey, TItem, TOther>(
        List<TItem> stored,
        Dictionary<TKey, TItem> target,
        Dictionary<TKey, TOther> opposite,
        Func<TItem, TKey> keyOf)
        where TKey : notnull
    {
        foreach (var item in stored)
        {
            var key = keyOf(item);
            if (!target.ContainsKey(key) && !opposite.ContainsKey(key))
                target[key] = item;
        }
    }

    // ── Budgets ──────────────────────────────────────────────────────────────

    public async Task<BudgetSet> LoadBudgetsAsync(CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        return new BudgetSet(_budgets.Values.ToList(), _budgetTombs.Values.ToList());
    }

    public async Task UpsertBudgetAsync(BudgetDto dto, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _budgets[dto.Name] = dto;
        _budgetTombs.Remove(dto.Name);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveBudgetAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _budgets.Remove(name);
        _budgetTombs[name] = new BudgetTombstone(name, deletedAtUtc);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllBudgetsAsync(BudgetSet set, CancellationToken ct = default)
    {
        _hydrated = true;
        _budgets.Clear();
        _budgetTombs.Clear();
        foreach (var b in set.Budgets)    _budgets[b.Name]     = b;
        foreach (var t in set.Tombstones) _budgetTombs[t.Name] = t;
        await PersistAsync(ct).ConfigureAwait(false);
    }

    // ── Transactions ─────────────────────────────────────────────────────────

    public async Task<TransactionSet> LoadTransactionsAsync(CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        return new TransactionSet(_transactions.Values.ToList(), _txTombs.Values.ToList());
    }

    public async Task UpsertTransactionAsync(TransactionDto dto, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _transactions[dto.Id] = dto;
        _txTombs.Remove(dto.Id);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveTransactionAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _transactions.Remove(id);
        _txTombs[id] = new TransactionTombstone(id, deletedAtUtc);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllTransactionsAsync(TransactionSet set, CancellationToken ct = default)
    {
        _hydrated = true;
        _transactions.Clear();
        _txTombs.Clear();
        foreach (var t in set.Transactions) _transactions[t.Id] = t;
        foreach (var t in set.Tombstones)   _txTombs[t.Id]      = t;
        await PersistAsync(ct).ConfigureAwait(false);
    }

    // ── Recurring bills ──────────────────────────────────────────────────────

    public async Task<RecurringBillSet> LoadRecurringBillsAsync(CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        return new RecurringBillSet(_bills.Values.ToList(), _billTombs.Values.ToList());
    }

    public async Task UpsertRecurringBillAsync(RecurringBillDto dto, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _bills[dto.Id] = dto;
        _billTombs.Remove(dto.Id);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveRecurringBillAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _bills.Remove(id);
        _billTombs[id] = new RecurringBillTombstone(id, deletedAtUtc);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllRecurringBillsAsync(RecurringBillSet set, CancellationToken ct = default)
    {
        _hydrated = true;
        _bills.Clear();
        _billTombs.Clear();
        foreach (var b in set.Bills)      _bills[b.Id]     = b;
        foreach (var t in set.Tombstones) _billTombs[t.Id] = t;
        await PersistAsync(ct).ConfigureAwait(false);
    }

    // ── Savings goals ────────────────────────────────────────────────────────

    public async Task<SavingsGoalSet> LoadSavingsGoalsAsync(CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        return new SavingsGoalSet(_goals.Values.ToList(), _goalTombs.Values.ToList());
    }

    public async Task UpsertSavingsGoalAsync(SavingsGoalDto dto, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _goals[dto.Id] = dto;
        _goalTombs.Remove(dto.Id);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveSavingsGoalAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _goals.Remove(id);
        _goalTombs[id] = new SavingsGoalTombstone(id, deletedAtUtc);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllSavingsGoalsAsync(SavingsGoalSet set, CancellationToken ct = default)
    {
        _hydrated = true;
        _goals.Clear();
        _goalTombs.Clear();
        foreach (var g in set.Goals)      _goals[g.Id]     = g;
        foreach (var t in set.Tombstones) _goalTombs[t.Id] = t;
        await PersistAsync(ct).ConfigureAwait(false);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private Task PersistAsync(CancellationToken ct)
    {
        var file = new StoreFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Budgets = _budgets.Values.ToList(),
            BudgetTombstones = _budgetTombs.Values.ToList(),
            Transactions = _transactions.Values.ToList(),
            TransactionTombstones = _txTombs.Values.ToList(),
            RecurringBills = _bills.Values.ToList(),
            RecurringBillTombstones = _billTombs.Values.ToList(),
            SavingsGoals = _goals.Values.ToList(),
            SavingsGoalTombstones = _goalTombs.Values.ToList()
        };
        return _storage.SetAsync(StorageKey, JsonSerializer.Serialize(file, PaycheckJson.Options), ct);
    }

    /// <summary>Envelope written to localStorage — mirrors the MAUI JSON file's shape.</summary>
    private sealed class StoreFile
    {
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
