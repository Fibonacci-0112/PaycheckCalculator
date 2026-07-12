using PaycheckCalculator.Core.Budgeting;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="BudgetReportCalculator"/>.
/// Expected values are derived from the documented rules, not recomputed from production helpers.
/// </summary>
public sealed class BudgetReportCalculatorTest
{
    private static readonly BudgetReportCalculator Calc = new();

    // A simple two-category budget: Housing $1,000 (Needs) + Food $400 (Needs).
    private static Budget TwoCategoryBudget(decimal monthlyIncome = 3_000m) => new()
    {
        Name = "Test Budget",
        MonthlyNetIncome = monthlyIncome,
        Categories =
        [
            new BudgetCategory { Name = "Housing", BudgetType = BudgetType.Needs, Amount = 1_000m, AmountType = BudgetAmountType.Dollar },
            new BudgetCategory { Name = "Food",    BudgetType = BudgetType.Needs, Amount = 400m,  AmountType = BudgetAmountType.Dollar },
        ]
    };

    private static BudgetTransaction Tx(string category, decimal amount, DateOnly date, string desc = "") =>
        new() { Id = Guid.NewGuid(), CategoryName = category, Amount = amount, Date = date, Description = desc };

    // ── MonthsBack = 0 ───────────────────────────────────────────────────────

    [Fact]
    public void MonthsBackZero_IncludesOnlyCurrentMonth()
    {
        var through = new DateOnly(2026, 6, 15);
        var report  = Calc.Compute(TwoCategoryBudget(), [], through, monthsBack: 0);

        Assert.Single(report.Months);
        Assert.Equal(new DateOnly(2026, 6, 1), report.Months[0]);
    }

    // ── Months list ───────────────────────────────────────────────────────────

    [Fact]
    public void MonthsBack5_Produces6Months_EarliestFirst()
    {
        var through = new DateOnly(2026, 6, 1);
        var report  = Calc.Compute(TwoCategoryBudget(), [], through, monthsBack: 5);

        Assert.Equal(6, report.Months.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), report.Months[0]);
        Assert.Equal(new DateOnly(2026, 6, 1), report.Months[5]);
    }

    [Fact]
    public void AllMonthValues_AreFirstDayOfMonth()
    {
        var through = new DateOnly(2026, 3, 31);
        var report  = Calc.Compute(TwoCategoryBudget(), [], through, monthsBack: 2);

        foreach (var m in report.Months)
            Assert.Equal(1, m.Day);
    }

    // ── SpendByCategory ───────────────────────────────────────────────────────

    [Fact]
    public void SingleMonth_TwoCategories_SpendPointsHaveCorrectAmounts()
    {
        var june = new DateOnly(2026, 6, 1);
        var transactions = new List<BudgetTransaction>
        {
            Tx("Housing", 950m,  new DateOnly(2026, 6, 5)),
            Tx("Housing", 50m,   new DateOnly(2026, 6, 6)),
            Tx("Food",    200m,  new DateOnly(2026, 6, 10)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), transactions, new DateOnly(2026, 6, 15), monthsBack: 0);

        var housingPt = report.SpendByCategory.Single(p => p.CategoryName == "Housing" && p.Month == june);
        var foodPt    = report.SpendByCategory.Single(p => p.CategoryName == "Food"    && p.Month == june);

        // Housing: 950 + 50 = 1,000.00
        Assert.Equal(1_000.00m, housingPt.Spent);
        Assert.Equal(1_000.00m, housingPt.Budgeted);

        // Food: 200.00
        Assert.Equal(200.00m, foodPt.Spent);
        Assert.Equal(400.00m, foodPt.Budgeted);
    }

    [Fact]
    public void MonthWithNoTransactions_SpentIsZero()
    {
        var report = Calc.Compute(TwoCategoryBudget(), [], new DateOnly(2026, 6, 15), monthsBack: 0);

        foreach (var pt in report.SpendByCategory)
            Assert.Equal(0m, pt.Spent);
    }

    [Fact]
    public void CategoryInTransactionButNotInBudget_ExcludedFromSpendPoints()
    {
        var txs = new List<BudgetTransaction>
        {
            Tx("Entertainment", 100m, new DateOnly(2026, 6, 1)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);

        Assert.DoesNotContain(report.SpendByCategory, p => p.CategoryName == "Entertainment");
    }

    [Fact]
    public void CategoryNameMatch_IsCaseInsensitive()
    {
        var txs = new List<BudgetTransaction>
        {
            Tx("HOUSING", 500m, new DateOnly(2026, 6, 1)),
            Tx("housing", 200m, new DateOnly(2026, 6, 2)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);

        var pt = report.SpendByCategory.Single(p =>
            string.Equals(p.CategoryName, "Housing", StringComparison.OrdinalIgnoreCase)
            && p.Month.Month == 6);

        // 500 + 200 = 700
        Assert.Equal(700m, pt.Spent);
    }

    [Fact]
    public void MultipleMonths_SpendPointsGroupedPerMonth()
    {
        var txs = new List<BudgetTransaction>
        {
            Tx("Food", 300m, new DateOnly(2026, 5, 10)),
            Tx("Food", 250m, new DateOnly(2026, 6, 12)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 1);

        var mayFood  = report.SpendByCategory.Single(p => p.CategoryName == "Food" && p.Month.Month == 5);
        var juneFood = report.SpendByCategory.Single(p => p.CategoryName == "Food" && p.Month.Month == 6);

        Assert.Equal(300m, mayFood.Spent);
        Assert.Equal(250m, juneFood.Spent);
    }

    [Fact]
    public void PercentageCategory_BudgetedComputedFromIncome()
    {
        // 30% of $4,000 = $1,200.00
        var budget = new Budget
        {
            Name = "Pct Budget",
            MonthlyNetIncome = 4_000m,
            Categories =
            [
                new BudgetCategory { Name = "Savings", BudgetType = BudgetType.Savings, Amount = 30m, AmountType = BudgetAmountType.Percentage }
            ]
        };

        var report = Calc.Compute(budget, [], new DateOnly(2026, 6, 1), monthsBack: 0);

        var pt = report.SpendByCategory.Single();
        Assert.Equal(1_200.00m, pt.Budgeted);
    }

    // ── BudgetVsActual ────────────────────────────────────────────────────────

    [Fact]
    public void TwoMonths_BudgetVsActualContainsOnePointPerMonth()
    {
        var report = Calc.Compute(TwoCategoryBudget(), [], new DateOnly(2026, 6, 15), monthsBack: 1);

        Assert.Equal(2, report.BudgetVsActual.Count);
    }

    [Fact]
    public void BudgetVsActual_BudgetedEqualsSumOfCategoryBudgets()
    {
        // Housing $1,000 + Food $400 = $1,400 budgeted per month
        var report = Calc.Compute(TwoCategoryBudget(), [], new DateOnly(2026, 6, 15), monthsBack: 0);

        Assert.Equal(1_400.00m, report.BudgetVsActual[0].Budgeted);
    }

    [Fact]
    public void BudgetVsActual_ActualEqualsSumOfAllTransactions()
    {
        var txs = new List<BudgetTransaction>
        {
            Tx("Housing", 1_000m, new DateOnly(2026, 6, 1)),
            Tx("Food",    350m,   new DateOnly(2026, 6, 15)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);

        Assert.Equal(1_350.00m, report.BudgetVsActual[0].Actual);
    }

    [Fact]
    public void BudgetVsActual_Variance_IsActualMinusBudgeted()
    {
        // Variance = Actual - Budgeted (always, by definition)
        var txs = new List<BudgetTransaction>
        {
            Tx("Housing", 1_000m, new DateOnly(2026, 6, 1)),
            Tx("Food",    350m,   new DateOnly(2026, 6, 5)),
        };
        // Actual = 1,350; Budgeted = 1,400; Variance = 1,350 - 1,400 = -50
        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);
        var pt     = report.BudgetVsActual[0];
        Assert.Equal(pt.Actual - pt.Budgeted, pt.Variance);
        Assert.Equal(-50.00m, pt.Variance);
    }

    [Fact]
    public void BudgetVsActual_NegativeVarianceWhenUnderBudget()
    {
        // Nothing spent → Actual = 0, Budgeted = 1,400 → Variance = 0 - 1,400 = -1,400
        var report = Calc.Compute(TwoCategoryBudget(), [], new DateOnly(2026, 6, 15), monthsBack: 0);

        Assert.Equal(-1_400.00m, report.BudgetVsActual[0].Variance);
    }

    [Fact]
    public void BudgetVsActual_PositiveVarianceWhenOverBudget()
    {
        // Spend $1,600 against $1,400 budget → Variance = 1,600 - 1,400 = +200
        var txs = new List<BudgetTransaction>
        {
            Tx("Housing", 1_200m, new DateOnly(2026, 6, 1)),
            Tx("Food",    400m,   new DateOnly(2026, 6, 5)),
        };
        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);
        Assert.Equal(200.00m, report.BudgetVsActual[0].Variance);
    }

    // ── GeneratedThrough & BudgetName ────────────────────────────────────────

    [Fact]
    public void Report_GeneratedThrough_MatchesInputDate()
    {
        var through = new DateOnly(2026, 6, 15);
        var report  = Calc.Compute(TwoCategoryBudget(), [], through, monthsBack: 0);

        Assert.Equal(through, report.GeneratedThrough);
    }

    [Fact]
    public void Report_BudgetName_MatchesBudget()
    {
        var report = Calc.Compute(TwoCategoryBudget(), [], new DateOnly(2026, 6, 1), monthsBack: 0);
        Assert.Equal("Test Budget", report.BudgetName);
    }

    // ── Rounding ─────────────────────────────────────────────────────────────

    [Fact]
    public void SpendPoint_Spent_RoundedToTwoCentsAwayFromZero()
    {
        // 3 transactions of $0.005 each = $0.015 → rounds away from zero to $0.02
        var txs = new List<BudgetTransaction>
        {
            Tx("Food", 0.005m, new DateOnly(2026, 6, 1)),
            Tx("Food", 0.005m, new DateOnly(2026, 6, 2)),
            Tx("Food", 0.005m, new DateOnly(2026, 6, 3)),
        };

        var report = Calc.Compute(TwoCategoryBudget(), txs, new DateOnly(2026, 6, 15), monthsBack: 0);

        var pt = report.SpendByCategory.Single(p => p.CategoryName == "Food" && p.Month.Month == 6);
        Assert.Equal(0.02m, pt.Spent);
    }
}
