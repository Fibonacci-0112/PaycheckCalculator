using System.Text.Json;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Saved-paycheck store for the Blazor app. Registered as scoped, so the in-memory working set
/// lives for the Blazor Server circuit, and each mutation is mirrored to the browser's
/// <c>localStorage</c> so anonymous data survives a refresh, a closed tab, and a new circuit —
/// the web equivalent of the MAUI app's on-device <c>JsonFilePaycheckStore</c>. Signing in syncs
/// the same data to the server, which retains it across devices.
///
/// Persistence is strictly best-effort (see <see cref="BrowserLocalStorage"/>): during
/// prerendering, on a disconnected circuit, or with storage blocked, this behaves exactly like
/// the pure in-memory store it replaced.
/// </summary>
public sealed class SessionPaycheckStore(BrowserLocalStorage storage) : ISavedPaycheckStore
{
    /// <summary>localStorage key. The trailing version is the envelope's, not a DTO's.</summary>
    internal const string StorageKey = "paycheckcalc.paychecks.v1";

    /// <summary>
    /// Highest envelope <see cref="StoreFile.SchemaVersion"/> this build understands. A payload
    /// written by a newer build is left untouched rather than parsed or overwritten.
    /// </summary>
    private const int CurrentSchemaVersion = 1;

    private readonly BrowserLocalStorage _storage = storage;
    private readonly Dictionary<string, SavedPaycheckDto> _paychecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedPaycheckTombstone> _tombstones = new(StringComparer.OrdinalIgnoreCase);
    private bool _hydrated;

    /// <summary>
    /// Loads persisted data into the in-memory set, at most once per circuit. Safe to call from
    /// anywhere: while the browser is unreachable it is a no-op and a later call retries, which is
    /// what lets the first (prerendered) render fall back to an empty set and the first
    /// interactive render pick the real data up.
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
        foreach (var dto in file.Paychecks)
            if (!_paychecks.ContainsKey(dto.Name) && !_tombstones.ContainsKey(dto.Name))
                _paychecks[dto.Name] = dto;
        foreach (var tomb in file.Tombstones)
            if (!_paychecks.ContainsKey(tomb.Name) && !_tombstones.ContainsKey(tomb.Name))
                _tombstones[tomb.Name] = tomb;
    }

    public async Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        return new SavedPaycheckSet(_paychecks.Values.ToList(), _tombstones.Values.ToList());
    }

    public async Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _paychecks[dto.Name] = dto;
        _tombstones.Remove(dto.Name);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        _paychecks.Remove(name);
        _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        await EnsureHydratedAsync(ct).ConfigureAwait(false);
        foreach (var name in _paychecks.Keys.ToList())
            _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        _paychecks.Clear();
        await PersistAsync(ct).ConfigureAwait(false);
    }

    public async Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default)
    {
        // A full replace (the post-sync merge result) is authoritative, so it deliberately does
        // not hydrate first — the incoming set already reflects everything the server knows.
        _hydrated = true;
        _paychecks.Clear();
        _tombstones.Clear();
        foreach (var dto in set.Paychecks) _paychecks[dto.Name] = dto;
        foreach (var tomb in set.Tombstones) _tombstones[tomb.Name] = tomb;
        await PersistAsync(ct).ConfigureAwait(false);
    }

    private Task PersistAsync(CancellationToken ct)
    {
        var file = new StoreFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Paychecks = _paychecks.Values.ToList(),
            Tombstones = _tombstones.Values.ToList()
        };
        return _storage.SetAsync(StorageKey, JsonSerializer.Serialize(file, PaycheckJson.Options), ct);
    }

    /// <summary>Envelope written to localStorage — mirrors the MAUI JSON file's shape.</summary>
    private sealed class StoreFile
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<SavedPaycheckDto> Paychecks { get; set; } = [];
        public List<SavedPaycheckTombstone> Tombstones { get; set; } = [];
    }
}
