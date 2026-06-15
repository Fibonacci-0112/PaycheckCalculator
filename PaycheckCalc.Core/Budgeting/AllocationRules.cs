namespace PaycheckCalc.Core.Budgeting;

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
}
