namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// Total budgeted vs total actually spent for a single month.
/// <see cref="Variance"/> is positive when over budget (Actual &gt; Budgeted), negative when under.
/// </summary>
public sealed class BudgetVsActualPoint
{
    /// <summary>First day of the calendar month this point covers.</summary>
    public DateOnly Month { get; init; }
    public decimal Budgeted { get; init; }
    public decimal Actual { get; init; }
    public decimal Variance => Actual - Budgeted;
}
