namespace PaycheckCalculator.Shared.Snapshots;

/// <summary>
/// Records that a paycheck with the given <see cref="Name"/> was deleted at <see cref="DeletedAtUtc"/>.
/// Tombstones are kept (even for anonymous users) so a delete propagates to other devices on the next
/// sync rather than being silently resurrected by a stale copy.
/// </summary>
public sealed record SavedPaycheckTombstone(string Name, DateTimeOffset DeletedAtUtc);
