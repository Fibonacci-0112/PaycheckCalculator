namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Spend vs budget for a single category in a single month.
/// </summary>
public sealed class SpendByCategoryPoint
{
    /// <summary>First day of the calendar month this point covers.</summary>
    public DateOnly Month { get; init; }
    public string CategoryName { get; init; } = "";
    public BudgetType BudgetType { get; init; }
    public decimal Spent { get; init; }
    public decimal Budgeted { get; init; }
}
