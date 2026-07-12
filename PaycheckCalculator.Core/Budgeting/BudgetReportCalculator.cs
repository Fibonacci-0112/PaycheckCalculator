namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// Aggregates transaction history into a read-only <see cref="BudgetReport"/>:
/// spend-by-category over time and budget-vs-actual trend.
/// Registered in DI via <c>AddPaycheckCalcCore</c>.
/// </summary>
public sealed class BudgetReportCalculator
{
    /// <summary>
    /// Computes the report for a range of calendar months.
    /// </summary>
    /// <param name="budget">Current budget definition (categories and monthly income).</param>
    /// <param name="allTransactions">All transactions across any date range (filtered internally by month).</param>
    /// <param name="through">The final month to include (inclusive); pass today's date to include the current month.</param>
    /// <param name="monthsBack">How many prior months to include before <paramref name="through"/> (0 = current month only).</param>
    public BudgetReport Compute(
        Budget budget,
        IReadOnlyList<BudgetTransaction> allTransactions,
        DateOnly through,
        int monthsBack = 5)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(allTransactions);
        if (monthsBack < 0) throw new ArgumentOutOfRangeException(nameof(monthsBack));

        var end   = new DateOnly(through.Year, through.Month, 1);
        var start = end.AddMonths(-monthsBack);

        var months = new List<DateOnly>(monthsBack + 1);
        for (var m = start; m <= end; m = m.AddMonths(1))
            months.Add(m);

        var spendPoints   = new List<SpendByCategoryPoint>(months.Count * budget.Categories.Count);
        var vsActualPoints = new List<BudgetVsActualPoint>(months.Count);

        foreach (var month in months)
        {
            var spentByCategory = allTransactions
                .Where(t => t.Date.Year == month.Year && t.Date.Month == month.Month)
                .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(t => t.Amount), 2, MidpointRounding.AwayFromZero),
                    StringComparer.OrdinalIgnoreCase);

            decimal monthBudgeted = 0m;
            decimal monthActual   = 0m;

            foreach (var cat in budget.Categories)
            {
                var budgeted = Math.Round(cat.EffectiveMonthlyBudget(budget.MonthlyNetIncome), 2, MidpointRounding.AwayFromZero);
                spentByCategory.TryGetValue(cat.Name, out var spent);

                spendPoints.Add(new SpendByCategoryPoint
                {
                    Month        = month,
                    CategoryName = cat.Name,
                    BudgetType   = cat.BudgetType,
                    Spent        = spent,
                    Budgeted     = budgeted,
                });

                monthBudgeted += budgeted;
                monthActual   += spent;
            }

            vsActualPoints.Add(new BudgetVsActualPoint
            {
                Month    = month,
                Budgeted = Math.Round(monthBudgeted, 2, MidpointRounding.AwayFromZero),
                Actual   = Math.Round(monthActual,   2, MidpointRounding.AwayFromZero),
            });
        }

        return new BudgetReport
        {
            BudgetName      = budget.Name,
            Months          = months,
            SpendByCategory = spendPoints,
            BudgetVsActual  = vsActualPoints,
            GeneratedThrough = through,
        };
    }
}
