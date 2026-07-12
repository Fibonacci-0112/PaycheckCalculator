using PaycheckCalculator.Shared.Snapshots;

namespace PaycheckCalculator.Shared.Sync;

/// <summary>
/// Where saved paychecks live for one client. MAUI backs this with a JSON file in app data; Blazor
/// backs it with circuit memory (so anonymous data dies when the tab closes). The sync orchestration
/// is identical regardless of backing store.
/// </summary>
public interface ISavedPaycheckStore
{
    /// <summary>Returns the current set (live entries + tombstones). Never throws for a missing/empty store.</summary>
    Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default);

    /// <summary>Inserts or replaces an entry by name (case-insensitive) and clears any matching tombstone.</summary>
    Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default);

    /// <summary>Removes the named entry and records a tombstone at <paramref name="deletedAtUtc"/>.</summary>
    Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default);

    /// <summary>Removes all entries, recording a tombstone for each at <paramref name="deletedAtUtc"/>.</summary>
    Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default);

    /// <summary>Replaces the entire store with <paramref name="set"/> (used after a sync merge).</summary>
    Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default);
}
