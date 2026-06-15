namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Computes a <see cref="BudgetSummary"/> for a given <see cref="Budget"/> and its month's
/// transactions. Per-category spent amounts are summed from the transaction list (case-insensitive
/// category name match); projected month-end is a linear extrapolation of the current run-rate
/// based on the day of the month. Registered in DI via <c>AddPaycheckCalcCore</c>.
/// </summary>
public sealed class BudgetCalculator
{
    /// <summary>
    /// Calculates the budget summary for the current month.
    /// </summary>
    /// <param name="budget">The budget definition (categories and monthly income).</param>
    /// <param name="transactions">All transactions for the current month.</param>
    /// <param name="today">The current date, used to compute the projected month-end run-rate.</param>
    public BudgetSummary Calculate(Budget budget, IReadOnlyList<BudgetTransaction> transactions, DateOnly today)
    {
        var monthlyIncome = budget.MonthlyNetIncome;

        var spentByCategory = transactions
            .GroupBy(t => t.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount), StringComparer.OrdinalIgnoreCase);

        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var daysElapsed = today.Day;

        var categories = budget.Categories.Select(cat =>
        {
            var budgeted = cat.EffectiveMonthlyBudget(monthlyIncome);
            spentByCategory.TryGetValue(cat.Name, out var spent);
            var projected = daysElapsed > 0
                ? Math.Round(spent / daysElapsed * daysInMonth, 2, MidpointRounding.AwayFromZero)
                : 0m;
            return new CategorySummary
            {
                Name = cat.Name,
                BudgetType = cat.BudgetType,
                Budgeted = budgeted,
                Spent = spent,
                ProjectedMonthEnd = projected
            };
        }).ToList();

        return new BudgetSummary
        {
            MonthlyNetIncome = monthlyIncome,
            TotalBudgeted = Math.Round(categories.Sum(c => c.Budgeted), 2, MidpointRounding.AwayFromZero),
            TotalSpent = Math.Round(categories.Sum(c => c.Spent), 2, MidpointRounding.AwayFromZero),
            Categories = categories
        };
    }
}
