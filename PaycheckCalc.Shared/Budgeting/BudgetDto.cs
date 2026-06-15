namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// A saved budget snapshot synced between devices. <see cref="Name"/> (case-insensitive) is the
/// logical identity for last-write-wins merge; <see cref="UpdatedAtUtc"/> drives conflict resolution.
/// </summary>
public sealed record BudgetDto
{
    public required string Name { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<BudgetCategoryDto> Categories { get; init; } = [];
    public decimal MonthlyNetIncome { get; init; }
}
