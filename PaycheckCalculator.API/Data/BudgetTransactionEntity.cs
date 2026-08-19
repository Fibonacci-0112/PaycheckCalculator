namespace PaycheckCalculator.API.Data;

/// <summary>
/// One stored budget transaction (or delete tombstone) for a user. The primary key is
/// (<see cref="UserId"/>, <see cref="Id"/>), where <see cref="Id"/> is the stable GUID
/// assigned when the transaction is first created on any client.
/// </summary>
public sealed class BudgetTransactionEntity
{
    public required string UserId { get; set; }
    public Guid Id { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Serialized <c>TransactionDto</c> for live entries; empty for tombstones.</summary>
    public string PayloadJson { get; set; } = "";
}
