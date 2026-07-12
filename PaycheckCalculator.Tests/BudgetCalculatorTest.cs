using PaycheckCalculator.Core.Budgeting;
using PaycheckCalculator.Core.Models;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="BudgetCalculator"/> and supporting helpers:
/// <see cref="AllocationRules"/>, <see cref="MonthlyIncomeNormalizer"/>, and
/// <see cref="BudgetCategory.EffectiveMonthlyBudget"/>.
/// Expected values are computed from the documented rules, never from the production helpers.
/// </summary>
public sealed class BudgetCalculatorTest
{
    private static readonly BudgetCalculator Calc = new();

    // ── MonthlyIncomeNormalizer ───────────────────────────────────────────────

    [Fact]
    public void ToMonthly_Biweekly_MultiplicandIs26DividedBy12()
    {
        // $1,000 × 26 / 12 = $2,166.67 (rounded away-from-zero)
        var result = MonthlyIncomeNormalizer.ToMonthly(1_000m, PayFrequency.Biweekly);
        Assert.Equal(2_166.67m, result);
    }

    [Fact]
    public void ToMonthly_Weekly_MultiplicandIs52DividedBy12()
    {
        // $500 × 52 / 12 = $2,166.67
        var result = MonthlyIncomeNormalizer.ToMonthly(500m, PayFrequency.Weekly);
        Assert.Equal(2_166.67m, result);
    }

    [Fact]
    public void ToMonthly_Semimonthly_MultiplicandIs24DividedBy12()
    {
        // $2,000 × 24 / 12 = $4,000.00
        var result = MonthlyIncomeNormalizer.ToMonthly(2_000m, PayFrequency.Semimonthly);
        Assert.Equal(4_000m, result);
    }

    [Fact]
    public void ToMonthly_Monthly_EqualsNetPay()
    {
        // $3,500 × 12 / 12 = $3,500.00
        var result = MonthlyIncomeNormalizer.ToMonthly(3_500m, PayFrequency.Monthly);
        Assert.Equal(3_500m, result);
    }

    [Fact]
    public void ToMonthly_Weekly53_MultiplicandIs53DividedBy12()
    {
        // $1,000 × 53 / 12 = $4,416.67 (rounded away-from-zero)
        var result = MonthlyIncomeNormalizer.ToMonthly(1_000m, PayFrequency.Weekly53);
        Assert.Equal(4_416.67m, result);
    }

    [Fact]
    public void ToMonthly_Biweekly27_MultiplicandIs27DividedBy12()
    {
        // $1,000 × 27 / 12 = $2,250.00
        var result = MonthlyIncomeNormalizer.ToMonthly(1_000m, PayFrequency.Biweekly27);
        Assert.Equal(2_250m, result);
    }

    // ── BudgetCategory.EffectiveMonthlyBudget ────────────────────────────────

    [Fact]
    public void EffectiveMonthlyBudget_Dollar_ReturnsAmount()
    {
        var cat = new BudgetCategory { Name = "Housing", Amount = 1_200m, AmountType = BudgetAmountType.Dollar };
        Assert.Equal(1_200m, cat.EffectiveMonthlyBudget(4_000m));
    }

    [Fact]
    public void EffectiveMonthlyBudget_Percentage50_Returns2000OnIncome4000()
    {
        // 50% × $4,000 = $2,000.00
        var cat = new BudgetCategory { Name = "Needs", Amount = 50m, AmountType = BudgetAmountType.Percentage };
        Assert.Equal(2_000m, cat.EffectiveMonthlyBudget(4_000m));
    }

    [Fact]
    public void EffectiveMonthlyBudget_Percentage30_Returns1200OnIncome4000()
    {
        // 30% × $4,000 = $1,200.00
        var cat = new BudgetCategory { Name = "Wants", Amount = 30m, AmountType = BudgetAmountType.Percentage };
        Assert.Equal(1_200m, cat.EffectiveMonthlyBudget(4_000m));
    }

    [Fact]
    public void EffectiveMonthlyBudget_Percentage20_Returns800OnIncome4000()
    {
        // 20% × $4,000 = $800.00
        var cat = new BudgetCategory { Name = "Savings", Amount = 20m, AmountType = BudgetAmountType.Percentage };
        Assert.Equal(800m, cat.EffectiveMonthlyBudget(4_000m));
    }

    [Fact]
    public void EffectiveMonthlyBudget_Percentage_RoundsAwayFromZero()
    {
        // 33.33% × $1,000 = $333.30 (333.3 rounds to 333.30, not 333.33)
        var cat = new BudgetCategory { Name = "X", Amount = 33.33m, AmountType = BudgetAmountType.Percentage };
        // 33.33 / 100 × 1000 = 333.3 → $333.30
        Assert.Equal(333.30m, cat.EffectiveMonthlyBudget(1_000m));
    }

    // ── AllocationRules.FiftyThirtyTwenty ────────────────────────────────────

    [Fact]
    public void FiftyThirtyTwenty_Returns3Categories()
    {
        var cats = AllocationRules.FiftyThirtyTwenty();
        Assert.Equal(3, cats.Count);
    }

    [Fact]
    public void FiftyThirtyTwenty_CorrectAllocations_On3000Income()
    {
        var cats = AllocationRules.FiftyThirtyTwenty();
        var income = 3_000m;

        // Needs: 50% × $3,000 = $1,500
        // Wants: 30% × $3,000 = $900
        // Savings: 20% × $3,000 = $600
        Assert.Equal(1_500m, cats[0].EffectiveMonthlyBudget(income));
        Assert.Equal(900m,   cats[1].EffectiveMonthlyBudget(income));
        Assert.Equal(600m,   cats[2].EffectiveMonthlyBudget(income));
    }

    [Fact]
    public void FiftyThirtyTwenty_BudgetTypes_AreCorrect()
    {
        var cats = AllocationRules.FiftyThirtyTwenty();
        Assert.Equal(BudgetType.Needs,   cats[0].BudgetType);
        Assert.Equal(BudgetType.Wants,   cats[1].BudgetType);
        Assert.Equal(BudgetType.Savings, cats[2].BudgetType);
    }

    // ── BudgetCalculator.Calculate ────────────────────────────────────────────

    [Fact]
    public void Calculate_NoTransactions_SpentIsZeroForAllCategories()
    {
        var budget = Budget50_30_20(4_000m);
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));

        // All categories have zero spent
        foreach (var cat in summary.Categories)
            Assert.Equal(0m, cat.Spent);
    }

    [Fact]
    public void Calculate_NoTransactions_TotalBudgetedMatchesIncome()
    {
        // 50 + 30 + 20 = 100% of $4,000 = $4,000
        var budget = Budget50_30_20(4_000m);
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));
        Assert.Equal(4_000m, summary.TotalBudgeted);
    }

    [Fact]
    public void Calculate_NeedsTransaction_CorrectlyAccumulatesSpent()
    {
        var budget = Budget50_30_20(4_000m);
        var transactions = new[]
        {
            Tx("Needs", 400m, new DateOnly(2026, 6, 5)),
            Tx("Needs", 200m, new DateOnly(2026, 6, 10))
        };
        var summary = Calc.Calculate(budget, transactions, new DateOnly(2026, 6, 15));

        var needs = summary.Categories.Single(c => c.Name == "Needs");
        Assert.Equal(600m, needs.Spent);  // 400 + 200
        Assert.Equal(2_000m, needs.Budgeted); // 50% × 4,000
        Assert.Equal(1_400m, needs.Remaining); // 2,000 - 600
    }

    [Fact]
    public void Calculate_CaseInsensitiveCategoryMatch()
    {
        var budget = Budget50_30_20(4_000m);
        // Category is "Needs" (capital N), transaction uses "needs" (lowercase)
        var transactions = new[] { Tx("needs", 500m, new DateOnly(2026, 6, 1)) };
        var summary = Calc.Calculate(budget, transactions, new DateOnly(2026, 6, 15));

        var needs = summary.Categories.Single(c => c.Name == "Needs");
        Assert.Equal(500m, needs.Spent);
    }

    [Fact]
    public void Calculate_ProjectedMonthEnd_LinearExtrapolation()
    {
        // Day 15 of a 30-day month, $600 spent → projected = $600 / 15 * 30 = $1,200
        var budget = Budget50_30_20(4_000m);
        var transactions = new[] { Tx("Needs", 600m, new DateOnly(2026, 6, 10)) };
        var summary = Calc.Calculate(budget, transactions, new DateOnly(2026, 6, 15));

        var needs = summary.Categories.Single(c => c.Name == "Needs");
        // 600 / 15 * 30 = 1,200.00
        Assert.Equal(1_200m, needs.ProjectedMonthEnd);
    }

    [Fact]
    public void Calculate_ProjectedMonthEnd_Day1_MultiplyByDaysInMonth()
    {
        // Day 1 of a 30-day month, $50 spent → projected = $50 / 1 * 30 = $1,500
        var budget = Budget50_30_20(4_000m);
        var transactions = new[] { Tx("Wants", 50m, new DateOnly(2026, 6, 1)) };
        var summary = Calc.Calculate(budget, transactions, new DateOnly(2026, 6, 1));

        var wants = summary.Categories.Single(c => c.Name == "Wants");
        // 50 / 1 * 30 = 1,500.00
        Assert.Equal(1_500m, wants.ProjectedMonthEnd);
    }

    [Fact]
    public void Calculate_TotalSpent_SumsAllCategories()
    {
        var budget = Budget50_30_20(4_000m);
        var transactions = new[]
        {
            Tx("Needs",   400m, new DateOnly(2026, 6, 5)),
            Tx("Wants",   200m, new DateOnly(2026, 6, 7)),
            Tx("Savings", 100m, new DateOnly(2026, 6, 9))
        };
        var summary = Calc.Calculate(budget, transactions, new DateOnly(2026, 6, 15));
        Assert.Equal(700m, summary.TotalSpent);
    }

    [Fact]
    public void Calculate_Unallocated_WhenDollarCategoriesDontSumToIncome()
    {
        // Budget with $1,500 in dollar categories on a $4,000 income → $2,500 unallocated
        var budget = new Budget
        {
            Name = "Test",
            MonthlyNetIncome = 4_000m,
            Categories =
            [
                new BudgetCategory { Name = "Housing", Amount = 1_000m, AmountType = BudgetAmountType.Dollar, BudgetType = BudgetType.Needs },
                new BudgetCategory { Name = "Food",    Amount = 500m,   AmountType = BudgetAmountType.Dollar, BudgetType = BudgetType.Needs }
            ]
        };
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));
        Assert.Equal(4_000m, summary.MonthlyNetIncome);
        Assert.Equal(1_500m, summary.TotalBudgeted);
        Assert.Equal(2_500m, summary.Unallocated);
    }

    // ── AllocationRules (zero-based / method templates) ──────────────────────

    [Fact]
    public void ZeroBasedStarter_AllCategoriesAreDollarSeededAtZero()
    {
        var cats = AllocationRules.ZeroBasedStarter();
        Assert.NotEmpty(cats);
        Assert.All(cats, c => Assert.Equal(BudgetAmountType.Dollar, c.AmountType));
        Assert.All(cats, c => Assert.Equal(0m, c.Amount));
    }

    [Fact]
    public void ForMethod_FiftyThirtyTwenty_ReturnsThreePercentageCategories()
    {
        var cats = AllocationRules.ForMethod(BudgetMethod.FiftyThirtyTwenty);
        Assert.Equal(3, cats.Count);
        Assert.All(cats, c => Assert.Equal(BudgetAmountType.Percentage, c.AmountType));
    }

    [Fact]
    public void ForMethod_Custom_ReturnsEmpty()
        => Assert.Empty(AllocationRules.ForMethod(BudgetMethod.Custom));

    // ── BudgetSummary.IsFullyAllocated (zero-based goal) ─────────────────────

    [Fact]
    public void IsFullyAllocated_True_WhenDollarCategoriesSumToIncome()
    {
        var budget = new Budget
        {
            Name = "ZB",
            MonthlyNetIncome = 3_000m,
            Method = BudgetMethod.ZeroBased,
            Categories =
            [
                new BudgetCategory { Name = "Housing", Amount = 2_000m, AmountType = BudgetAmountType.Dollar, BudgetType = BudgetType.Needs },
                new BudgetCategory { Name = "Savings", Amount = 1_000m, AmountType = BudgetAmountType.Dollar, BudgetType = BudgetType.Savings }
            ]
        };
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));
        Assert.Equal(0m, summary.Unallocated);
        Assert.True(summary.IsFullyAllocated);
    }

    [Fact]
    public void IsFullyAllocated_False_WhenIncomeRemainsUnassigned()
    {
        var budget = new Budget
        {
            Name = "ZB",
            MonthlyNetIncome = 3_000m,
            Categories = [new BudgetCategory { Name = "Housing", Amount = 2_000m, AmountType = BudgetAmountType.Dollar, BudgetType = BudgetType.Needs }]
        };
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));
        Assert.Equal(1_000m, summary.Unallocated);
        Assert.False(summary.IsFullyAllocated);
    }

    // ── BudgetCalculator with recurring bills & savings goals ────────────────

    [Fact]
    public void Calculate_RecurringBills_FoldIntoCategoryAndTotalRecurring()
    {
        var budget = Budget50_30_20(4_000m);
        RecurringBill[] bills =
        [
            new() { Name = "Rent",    CategoryName = "Needs", Amount = 1_500m, Frequency = RecurrenceFrequency.Monthly },
            new() { Name = "Insurance", CategoryName = "Needs", Amount = 1_200m, Frequency = RecurrenceFrequency.Annual } // $100/mo
        ];
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15), bills);

        var needs = summary.Categories.Single(c => c.Name == "Needs");
        Assert.Equal(1_600m, needs.Recurring);       // 1,500 + 100
        Assert.Equal(1_600m, summary.TotalRecurring);
    }

    [Fact]
    public void Calculate_SavingsGoals_SumIntoTotalSavingsContribution()
    {
        var budget = Budget50_30_20(4_000m);
        SavingsGoal[] goals =
        [
            new() { Name = "Vacation", TargetAmount = 6_000m, CurrentAmount = 0m, TargetDate = new DateOnly(2026, 12, 15) }, // $1,000/mo
            new() { Name = "Laptop",   TargetAmount = 3_000m, CurrentAmount = 0m, TargetDate = new DateOnly(2026, 9, 15) }   // $1,000/mo
        ];
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15), recurringBills: null, savingsGoals: goals);

        Assert.Equal(2_000m, summary.TotalSavingsContribution);
    }

    [Fact]
    public void Calculate_NoBillsOrGoals_TotalsAreZero()
    {
        var budget = Budget50_30_20(4_000m);
        var summary = Calc.Calculate(budget, [], new DateOnly(2026, 6, 15));
        Assert.Equal(0m, summary.TotalRecurring);
        Assert.Equal(0m, summary.TotalSavingsContribution);
        Assert.All(summary.Categories, c => Assert.Equal(0m, c.Recurring));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Budget Budget50_30_20(decimal monthlyNetIncome) => new()
    {
        Name = "Test",
        MonthlyNetIncome = monthlyNetIncome,
        Categories = AllocationRules.FiftyThirtyTwenty()
            .Select(c => new BudgetCategory
            {
                Name = c.Name, BudgetType = c.BudgetType,
                Amount = c.Amount, AmountType = c.AmountType
            }).ToList()
    };

    private static BudgetTransaction Tx(string category, decimal amount, DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        CategoryName = category,
        Amount = amount,
        Date = date
    };
}
