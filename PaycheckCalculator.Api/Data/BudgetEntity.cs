namespace PaycheckCalculator.Api.Data;

/// <summary>
/// One stored budget (or delete tombstone) for a user. The primary key is
/// (<see cref="UserId"/>, <see cref="NameKey"/>), where <see cref="NameKey"/> is the lower-invariant
/// budget name — so names are unique per user case-insensitively, matching the merge semantics.
/// </summary>
public sealed class BudgetEntity
{
    public required string UserId { get; set; }

    /// <summary>Lower-invariant budget name; the case-insensitive identity within a user.</summary>
    public required string NameKey { get; set; }

    /// <summary>The display name with original casing.</summary>
    public required string Name { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Serialized <c>BudgetDto</c> for live entries; empty for tombstones.</summary>
    public string PayloadJson { get; set; } = "";
}
