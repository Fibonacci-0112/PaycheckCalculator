namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// Aggregate result from <see cref="BudgetCalculator"/>: monthly income, total budgeted,
/// total spent, unallocated income, overall remaining, and per-category breakdowns.
/// </summary>
public sealed class BudgetSummary
{
    public decimal MonthlyNetIncome { get; init; }
    public decimal TotalBudgeted { get; init; }
    public decimal TotalSpent { get; init; }

    /// <summary>Total monthly-equivalent cost of all recurring bills.</summary>
    public decimal TotalRecurring { get; init; }

    /// <summary>Total monthly contribution required across all savings goals.</summary>
    public decimal TotalSavingsContribution { get; init; }

    /// <summary>Income not assigned to any category (MonthlyNetIncome − TotalBudgeted).</summary>
    public decimal Unallocated => MonthlyNetIncome - TotalBudgeted;

    /// <summary>
    /// True when every dollar of monthly income is assigned to a category — the goal of a zero-based
    /// budget. Uses a one-cent tolerance to absorb rounding.
    /// </summary>
    public bool IsFullyAllocated => Math.Abs(Unallocated) <= 0.01m;

    /// <summary>How much of the total budget has not yet been spent (TotalBudgeted − TotalSpent).</summary>
    public decimal Remaining => TotalBudgeted - TotalSpent;

    public IReadOnlyList<CategorySummary> Categories { get; init; } = [];
}
