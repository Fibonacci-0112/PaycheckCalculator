using PaycheckCalculator.Core.Budgeting;

namespace PaycheckCalculator.Shared.Budgeting;

public sealed record BudgetCategoryDto
{
    public required string Name { get; init; }
    public BudgetType BudgetType { get; init; }
    public decimal Amount { get; init; }
    public BudgetAmountType AmountType { get; init; } = BudgetAmountType.Dollar;
}
