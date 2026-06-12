namespace PaycheckCalc.Shared.Snapshots;

/// <summary>The complete saved state for one user/device: live paychecks plus delete tombstones.</summary>
public sealed record SavedPaycheckSet(
    IReadOnlyList<SavedPaycheckDto> Paychecks,
    IReadOnlyList<SavedPaycheckTombstone> Tombstones)
{
    public static SavedPaycheckSet Empty { get; } =
        new(Array.Empty<SavedPaycheckDto>(), Array.Empty<SavedPaycheckTombstone>());
}
