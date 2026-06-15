using System.Text.Json;
using PaycheckCalc.Shared.Budgeting;
using PaycheckCalc.Shared.Json;

namespace PaycheckCalc.App.Services.Storage;

/// <summary>
/// Persists budgets and transactions to a JSON file in the app's private data directory, so they
/// survive app restarts even without an account. All reads/writes are serialized behind a semaphore;
/// a corrupt file is moved aside rather than crashing startup.
/// </summary>
public sealed class JsonFileBudgetStore : IBudgetStore
{
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "budgets.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BudgetSet> LoadBudgetsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            return new BudgetSet(file.Budgets, file.BudgetTombstones);
        }
        finally { _gate.Release(); }
    }

    public Task UpsertBudgetAsync(BudgetDto dto, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Budgets.RemoveAll(b => NameEquals(b.Name, dto.Name));
            file.BudgetTombstones.RemoveAll(t => NameEquals(t.Name, dto.Name));
            file.Budgets.Add(dto);
        }, ct);

    public Task RemoveBudgetAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Budgets.RemoveAll(b => NameEquals(b.Name, name));
            file.BudgetTombstones.RemoveAll(t => NameEquals(t.Name, name));
            file.BudgetTombstones.Add(new BudgetTombstone(name, deletedAtUtc));
        }, ct);

    public Task ReplaceAllBudgetsAsync(BudgetSet set, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Budgets = set.Budgets.ToList();
            file.BudgetTombstones = set.Tombstones.ToList();
        }, ct);

    public async Task<TransactionSet> LoadTransactionsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            return new TransactionSet(file.Transactions, file.TransactionTombstones);
        }
        finally { _gate.Release(); }
    }

    public Task UpsertTransactionAsync(TransactionDto dto, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Transactions.RemoveAll(t => t.Id == dto.Id);
            file.TransactionTombstones.RemoveAll(t => t.Id == dto.Id);
            file.Transactions.Add(dto);
        }, ct);

    public Task RemoveTransactionAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Transactions.RemoveAll(t => t.Id == id);
            file.TransactionTombstones.RemoveAll(t => t.Id == id);
            file.TransactionTombstones.Add(new TransactionTombstone(id, deletedAtUtc));
        }, ct);

    public Task ReplaceAllTransactionsAsync(TransactionSet set, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Transactions = set.Transactions.ToList();
            file.TransactionTombstones = set.Tombstones.ToList();
        }, ct);

    public async Task<RecurringBillSet> LoadRecurringBillsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            return new RecurringBillSet(file.RecurringBills, file.RecurringBillTombstones);
        }
        finally { _gate.Release(); }
    }

    public Task UpsertRecurringBillAsync(RecurringBillDto dto, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.RecurringBills.RemoveAll(b => b.Id == dto.Id);
            file.RecurringBillTombstones.RemoveAll(t => t.Id == dto.Id);
            file.RecurringBills.Add(dto);
        }, ct);

    public Task RemoveRecurringBillAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.RecurringBills.RemoveAll(b => b.Id == id);
            file.RecurringBillTombstones.RemoveAll(t => t.Id == id);
            file.RecurringBillTombstones.Add(new RecurringBillTombstone(id, deletedAtUtc));
        }, ct);

    public Task ReplaceAllRecurringBillsAsync(RecurringBillSet set, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.RecurringBills = set.Bills.ToList();
            file.RecurringBillTombstones = set.Tombstones.ToList();
        }, ct);

    public async Task<SavingsGoalSet> LoadSavingsGoalsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            return new SavingsGoalSet(file.SavingsGoals, file.SavingsGoalTombstones);
        }
        finally { _gate.Release(); }
    }

    public Task UpsertSavingsGoalAsync(SavingsGoalDto dto, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.SavingsGoals.RemoveAll(g => g.Id == dto.Id);
            file.SavingsGoalTombstones.RemoveAll(t => t.Id == dto.Id);
            file.SavingsGoals.Add(dto);
        }, ct);

    public Task RemoveSavingsGoalAsync(Guid id, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.SavingsGoals.RemoveAll(g => g.Id == id);
            file.SavingsGoalTombstones.RemoveAll(t => t.Id == id);
            file.SavingsGoalTombstones.Add(new SavingsGoalTombstone(id, deletedAtUtc));
        }, ct);

    public Task ReplaceAllSavingsGoalsAsync(SavingsGoalSet set, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.SavingsGoals = set.Goals.ToList();
            file.SavingsGoalTombstones = set.Tombstones.ToList();
        }, ct);

    private async Task MutateAsync(Action<StoreFile> mutate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            mutate(file);
            var json = JsonSerializer.Serialize(file, PaycheckJson.Options);
            await File.WriteAllTextAsync(_filePath, json, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<StoreFile> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new StoreFile();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<StoreFile>(json, PaycheckJson.Options) ?? new StoreFile();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            TryBackupCorruptFile();
            return new StoreFile();
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Move(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch { }
    }

    private static bool NameEquals(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed class StoreFile
    {
        public int SchemaVersion { get; set; } = 2;
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
