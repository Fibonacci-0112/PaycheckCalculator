using PaycheckCalc.Core.Budgeting;

namespace PaycheckCalc.Shared.Budgeting;

/// <summary>
/// A saved budget snapshot synced between devices. <see cref="Name"/> (case-insensitive) is the
/// logical identity for last-write-wins merge; <see cref="UpdatedAtUtc"/> drives conflict resolution.
/// SchemaVersion 2 (C2) adds <see cref="Method"/>; recurring bills and savings goals sync as their
/// own GUID-keyed sets (see <see cref="RecurringBillDto"/> / <see cref="SavingsGoalDto"/>).
/// </summary>
public sealed record BudgetDto
{
    public required string Name { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public int SchemaVersion { get; init; } = 2;
    public IReadOnlyList<BudgetCategoryDto> Categories { get; init; } = [];
    public decimal MonthlyNetIncome { get; init; }
    public BudgetMethod Method { get; init; } = BudgetMethod.Custom;
}
