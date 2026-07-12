namespace PaycheckCalculator.Shared.Budgeting;

/// <summary>
/// A savings goal synced between devices. <see cref="Id"/> is a stable GUID and the sync key for
/// last-write-wins merge; <see cref="BudgetName"/> links the goal to its parent budget.
/// </summary>
public sealed record SavingsGoalDto
{
    public Guid Id { get; init; }
    public required string BudgetName { get; init; }
    public required string Name { get; init; }
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
