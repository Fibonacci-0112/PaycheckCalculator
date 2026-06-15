namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// A named monthly budget, carrying its category definitions and the monthly net income they
/// are allocated against. The name is the user-facing label and is the case-insensitive sync key.
/// </summary>
public sealed class Budget
{
    public string Name { get; init; } = "";
    public IReadOnlyList<BudgetCategory> Categories { get; init; } = [];
    public decimal MonthlyNetIncome { get; init; }
}
