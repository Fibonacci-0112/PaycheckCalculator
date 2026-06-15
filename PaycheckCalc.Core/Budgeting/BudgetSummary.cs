namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Aggregate result from <see cref="BudgetCalculator"/>: monthly income, total budgeted,
/// total spent, unallocated income, overall remaining, and per-category breakdowns.
/// </summary>
public sealed class BudgetSummary
{
    public decimal MonthlyNetIncome { get; init; }
    public decimal TotalBudgeted { get; init; }
    public decimal TotalSpent { get; init; }

    /// <summary>Income not assigned to any category (MonthlyNetIncome − TotalBudgeted).</summary>
    public decimal Unallocated => MonthlyNetIncome - TotalBudgeted;

    /// <summary>How much of the total budget has not yet been spent (TotalBudgeted − TotalSpent).</summary>
    public decimal Remaining => TotalBudgeted - TotalSpent;

    public IReadOnlyList<CategorySummary> Categories { get; init; } = [];
}
