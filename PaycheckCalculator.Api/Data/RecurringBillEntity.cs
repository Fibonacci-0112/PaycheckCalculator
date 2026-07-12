namespace PaycheckCalculator.Api.Data;

/// <summary>
/// One stored recurring bill (or delete tombstone) for a user. The primary key is
/// (<see cref="UserId"/>, <see cref="Id"/>), where <see cref="Id"/> is the stable GUID assigned
/// when the bill is first created on any client.
/// </summary>
public sealed class RecurringBillEntity
{
    public required string UserId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Serialized <c>RecurringBillDto</c> for live entries; empty for tombstones.</summary>
    public string PayloadJson { get; set; } = "";
}
