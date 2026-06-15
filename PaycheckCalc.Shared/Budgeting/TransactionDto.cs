namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// A single recorded expense synced between devices.
/// <see cref="Id"/> is a stable GUID and the sync key for last-write-wins merge.
/// </summary>
public sealed record TransactionDto
{
    public Guid Id { get; init; }
    public required string BudgetName { get; init; }
    public required string CategoryName { get; init; }
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public string Description { get; init; } = "";
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
