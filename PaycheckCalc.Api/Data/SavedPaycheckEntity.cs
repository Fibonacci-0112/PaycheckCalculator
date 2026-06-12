namespace PaycheckCalc.Api.Data;

/// <summary>
/// One stored paycheck (or delete tombstone) for a user. The primary key is
/// (<see cref="UserId"/>, <see cref="NameKey"/>), where <see cref="NameKey"/> is the lower-invariant
/// paycheck name — so names are unique per user case-insensitively, matching the merge semantics.
/// </summary>
public sealed class SavedPaycheckEntity
{
    public required string UserId { get; set; }

    /// <summary>Lower-invariant paycheck name; the case-insensitive identity within a user.</summary>
    public required string NameKey { get; set; }

    /// <summary>The display name with original casing.</summary>
    public required string Name { get; set; }

    /// <summary>Last-write time for a live entry, or the delete time for a tombstone.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>True when this row is a tombstone (the paycheck was deleted).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Serialized <c>SavedPaycheckDto</c> for live entries; empty for tombstones.</summary>
    public string PayloadJson { get; set; } = "";
}
