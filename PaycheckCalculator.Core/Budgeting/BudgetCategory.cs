namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// A named budget category with a monthly allocation that can be expressed as a fixed dollar
/// amount or as a percentage of monthly net income, mirroring <c>Deduction.EffectiveAmount</c>.
/// </summary>
public sealed class BudgetCategory
{
    public string Name { get; init; } = "";
    public BudgetType BudgetType { get; init; }
    public decimal Amount { get; init; }
    public BudgetAmountType AmountType { get; init; } = BudgetAmountType.Dollar;

    /// <summary>
    /// Effective monthly allocation for this category.
    /// Dollar categories return <see cref="Amount"/> directly; percentage categories compute
    /// <c>Amount / 100 × monthlyNetIncome</c>, rounded to the cent (away-from-zero).
    /// </summary>
    public decimal EffectiveMonthlyBudget(decimal monthlyNetIncome) => AmountType switch
    {
        BudgetAmountType.Percentage =>
            Math.Round(Amount / 100m * monthlyNetIncome, 2, MidpointRounding.AwayFromZero),
        _ => Amount
    };
}
