using PaycheckCalculator.Shared.Client;
using PaycheckCalculator.Shared.Snapshots;

namespace PaycheckCalculator.Shared.Sync;

/// <summary>Result of a sync attempt: the merged set on success, or an error message.</summary>
public sealed record SyncOutcome(bool Success, SavedPaycheckSet? Merged, string? Error)
{
    public static SyncOutcome Ok(SavedPaycheckSet merged) => new(true, merged, null);
    public static SyncOutcome Fail(string error) => new(false, null, error);
}

/// <summary>
/// Orchestrates a one-shot sync: load the local store, push it to the API, and replace local state
/// with the server-merged result. The merge itself runs server-side (see <see cref="SavedPaycheckMerger"/>),
/// so clients never need their own conflict resolution.
/// </summary>
public sealed class PaycheckSyncService
{
    private readonly ISavedPaycheckStore _store;
    private readonly PaycheckApiClient _api;

    public PaycheckSyncService(ISavedPaycheckStore store, PaycheckApiClient api)
    {
        _store = store;
        _api = api;
    }

    public async Task<SyncOutcome> SyncAsync(CancellationToken ct = default)
    {
        var local = await _store.LoadAsync(ct).ConfigureAwait(false);
        var result = await _api.SyncAsync(SyncRequest.From(local), ct).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            return SyncOutcome.Fail(result.Error ?? "Sync failed.");

        var merged = result.Value.ToSet();
        await _store.ReplaceAllAsync(merged, ct).ConfigureAwait(false);
        return SyncOutcome.Ok(merged);
    }
}
