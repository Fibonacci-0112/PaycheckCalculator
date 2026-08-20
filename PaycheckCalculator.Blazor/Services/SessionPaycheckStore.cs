using System.Text.Json;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Saved-paycheck store for the Blazor app. The authoritative copy lives in memory for the lifetime
/// of the Blazor Server circuit; it is additionally mirrored into the browser's <c>localStorage</c>
/// so anonymous (signed-out) paychecks survive closing the tab, matching the on-device persistence
/// the MAUI app already provides. Signing in syncs this data to the server.
/// <para>
/// Persistence is best-effort: JS interop is unavailable during prerendering and
/// <c>localStorage</c> can be blocked outright, so the store keeps working in-memory either way.
/// Pages call <see cref="HydrateAsync"/> once interactive rendering has started.
/// </para>
/// </summary>
public sealed class SessionPaycheckStore(BrowserLocalStorage storage) : ISavedPaycheckStore
{
    /// <summary>Browser storage key for the anonymous paycheck snapshot.</summary>
    internal const string StorageKey = "paycheckcalculator.paychecks";

    private readonly BrowserLocalStorage _storage = storage;
    private readonly Dictionary<string, SavedPaycheckDto> _paychecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedPaycheckTombstone> _tombstones = new(StringComparer.OrdinalIgnoreCase);
    private bool _hydrated;

    /// <summary>
    /// Loads any previously persisted paychecks from browser storage into this circuit. Safe to call
    /// repeatedly — only the first successful call populates the store, so a later hydration can
    /// never clobber data the user entered in this session. Returns true when data was restored.
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

        foreach (var dto in file.Paychecks) _paychecks[dto.Name] = dto;
        foreach (var tomb in file.Tombstones) _tombstones[tomb.Name] = tomb;
        return _paychecks.Count > 0;
    }

    public Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(new SavedPaycheckSet(_paychecks.Values.ToList(), _tombstones.Values.ToList()));

    public Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default)
    {
        _paychecks[dto.Name] = dto;
        _tombstones.Remove(dto.Name);
        return PersistAsync(ct);
    }

    public Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _paychecks.Remove(name);
        _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        return PersistAsync(ct);
    }

    public Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        foreach (var name in _paychecks.Keys.ToList())
            _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        _paychecks.Clear();
        return PersistAsync(ct);
    }

    public Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default)
    {
        _paychecks.Clear();
        _tombstones.Clear();
        foreach (var dto in set.Paychecks) _paychecks[dto.Name] = dto;
        foreach (var tomb in set.Tombstones) _tombstones[tomb.Name] = tomb;
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
            Paychecks = _paychecks.Values.ToList(),
            Tombstones = _tombstones.Values.ToList()
        };

        await _storage.SetAsync(StorageKey, JsonSerializer.Serialize(file, PaycheckJson.Options), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Versioned envelope, mirroring the MAUI <c>JsonFilePaycheckStore</c> file format so both
    /// clients persist the same shape.
    /// </summary>
    private sealed class StoreFile
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<SavedPaycheckDto> Paychecks { get; set; } = [];
        public List<SavedPaycheckTombstone> Tombstones { get; set; } = [];
    }
}
