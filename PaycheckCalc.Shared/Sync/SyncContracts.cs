using PaycheckCalc.Shared.Snapshots;

namespace PaycheckCalc.Shared.Sync;

/// <summary>A client's full saved state pushed to the server for merging.</summary>
public sealed record SyncRequest(
    IReadOnlyList<SavedPaycheckDto> Paychecks,
    IReadOnlyList<SavedPaycheckTombstone> Tombstones)
{
    public static SyncRequest From(SavedPaycheckSet set) => new(set.Paychecks, set.Tombstones);
}

/// <summary>The merged state the server returns; the client replaces its local state with this.</summary>
public sealed record SyncResponse(
    IReadOnlyList<SavedPaycheckDto> Paychecks,
    IReadOnlyList<SavedPaycheckTombstone> Tombstones,
    DateTimeOffset ServerTimeUtc)
{
    public SavedPaycheckSet ToSet() => new(Paychecks, Tombstones);
}
