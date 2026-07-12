using System.Text.Json;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.App.Services.Storage;

/// <summary>
/// Persists saved paychecks to a JSON file in the app's private data directory, so they survive app
/// restarts even without an account. All reads/writes are serialized behind a semaphore (read-modify-
/// write); a corrupt file is moved aside rather than crashing startup.
/// </summary>
public sealed class JsonFilePaycheckStore : ISavedPaycheckStore
{
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "saved-paychecks.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var file = await ReadAsync(ct).ConfigureAwait(false);
            return new SavedPaycheckSet(file.Paychecks, file.Tombstones);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Paychecks.RemoveAll(p => NameEquals(p.Name, dto.Name));
            file.Tombstones.RemoveAll(t => NameEquals(t.Name, dto.Name));
            file.Paychecks.Add(dto);
        }, ct);

    public Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Paychecks.RemoveAll(p => NameEquals(p.Name, name));
            file.Tombstones.RemoveAll(t => NameEquals(t.Name, name));
            file.Tombstones.Add(new SavedPaycheckTombstone(name, deletedAtUtc));
        }, ct);

    public Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            foreach (var p in file.Paychecks)
                if (!file.Tombstones.Any(t => NameEquals(t.Name, p.Name)))
                    file.Tombstones.Add(new SavedPaycheckTombstone(p.Name, deletedAtUtc));
            file.Paychecks.Clear();
        }, ct);

    public Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default)
        => MutateAsync(file =>
        {
            file.Paychecks = set.Paychecks.ToList();
            file.Tombstones = set.Tombstones.ToList();
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
        finally
        {
            _gate.Release();
        }
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
        catch
        {
            // Best-effort; never let a backup failure crash the app.
        }
    }

    private static bool NameEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed class StoreFile
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SavedPaycheckDto> Paychecks { get; set; } = new();
        public List<SavedPaycheckTombstone> Tombstones { get; set; } = new();
    }
}
