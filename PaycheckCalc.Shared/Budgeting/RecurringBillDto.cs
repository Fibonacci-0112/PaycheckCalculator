using PaycheckCalc.Core.Budgeting;

namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// A recurring bill synced between devices. <see cref="Id"/> is a stable GUID and the sync key for
/// last-write-wins merge; <see cref="BudgetName"/> links the bill to its parent budget.
/// </summary>
public sealed record RecurringBillDto
{
    public Guid Id { get; init; }
    public required string BudgetName { get; init; }
    public required string Name { get; init; }
    public required string CategoryName { get; init; }
    public decimal Amount { get; init; }
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;
    public int? DueDayOfMonth { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
