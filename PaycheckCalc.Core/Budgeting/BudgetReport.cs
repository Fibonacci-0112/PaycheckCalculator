namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Read-only aggregate report produced by <see cref="BudgetReportCalculator"/>.
/// Contains spend-by-category history and budget-vs-actual trend across a range of months.
/// </summary>
public sealed class BudgetReport
{
    public string BudgetName { get; init; } = "";

    /// <summary>Calendar months covered, earliest first (each value is the first day of that month).</summary>
    public IReadOnlyList<DateOnly> Months { get; init; } = [];

    /// <summary>One entry per (month, category) pair for every category defined in the budget.</summary>
    public IReadOnlyList<SpendByCategoryPoint> SpendByCategory { get; init; } = [];

    /// <summary>One entry per month: total budgeted vs total actually spent.</summary>
    public IReadOnlyList<BudgetVsActualPoint> BudgetVsActual { get; init; } = [];

    public DateOnly GeneratedThrough { get; init; }
}
