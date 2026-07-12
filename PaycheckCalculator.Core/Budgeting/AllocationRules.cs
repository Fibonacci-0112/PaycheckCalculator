namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// Pre-built budget allocation rules. Each rule returns a set of <see cref="BudgetCategory"/>s
/// whose allocations sum to 100% and are expressed as percentages of monthly net income, so
/// <see cref="BudgetCategory.EffectiveMonthlyBudget"/> scales automatically as income changes.
/// </summary>
public static class AllocationRules
{
    /// <summary>
    /// The classic 50/30/20 rule: 50% Needs, 30% Wants, 20% Savings.
    /// </summary>
    public static IReadOnlyList<BudgetCategory> FiftyThirtyTwenty() =>
    [
        new BudgetCategory { Name = "Needs",   BudgetType = BudgetType.Needs,   Amount = 50m, AmountType = BudgetAmountType.Percentage },
        new BudgetCategory { Name = "Wants",   BudgetType = BudgetType.Wants,   Amount = 30m, AmountType = BudgetAmountType.Percentage },
        new BudgetCategory { Name = "Savings", BudgetType = BudgetType.Savings, Amount = 20m, AmountType = BudgetAmountType.Percentage }
    ];

    /// <summary>
    /// A zero-based starter: common dollar-amount categories seeded at $0 for the user to assign every
    /// dollar of income to. Unlike <see cref="FiftyThirtyTwenty"/> these are fixed-dollar so the goal is
    /// for <see cref="BudgetSummary.Unallocated"/> to reach exactly zero.
    /// </summary>
    public static IReadOnlyList<BudgetCategory> ZeroBasedStarter() =>
    [
        new BudgetCategory { Name = "Housing",        BudgetType = BudgetType.Needs,   Amount = 0m, AmountType = BudgetAmountType.Dollar },
        new BudgetCategory { Name = "Food",           BudgetType = BudgetType.Needs,   Amount = 0m, AmountType = BudgetAmountType.Dollar },
        new BudgetCategory { Name = "Transportation", BudgetType = BudgetType.Needs,   Amount = 0m, AmountType = BudgetAmountType.Dollar },
        new BudgetCategory { Name = "Utilities",      BudgetType = BudgetType.Needs,   Amount = 0m, AmountType = BudgetAmountType.Dollar },
        new BudgetCategory { Name = "Discretionary",  BudgetType = BudgetType.Wants,   Amount = 0m, AmountType = BudgetAmountType.Dollar },
        new BudgetCategory { Name = "Savings",        BudgetType = BudgetType.Savings, Amount = 0m, AmountType = BudgetAmountType.Dollar }
    ];

    /// <summary>Returns the starter categories for a given <see cref="BudgetMethod"/>.</summary>
    public static IReadOnlyList<BudgetCategory> ForMethod(BudgetMethod method) => method switch
    {
        BudgetMethod.FiftyThirtyTwenty => FiftyThirtyTwenty(),
        BudgetMethod.ZeroBased or BudgetMethod.Envelope => ZeroBasedStarter(),
        _ => []
    };
}
