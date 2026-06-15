namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Per-category result from <see cref="BudgetCalculator"/>: how much was budgeted, spent,
/// is remaining, and what month-end spending is projected based on the current run-rate.
/// </summary>
public sealed class CategorySummary
{
    public required string Name { get; init; }
    public BudgetType BudgetType { get; init; }
    public decimal Budgeted { get; init; }
    public decimal Spent { get; init; }
    public decimal Remaining => Budgeted - Spent;
    public decimal ProjectedMonthEnd { get; init; }
}
