namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// A single recorded expense assigned to a named budget category.
/// The <see cref="Id"/> is a stable GUID used as the sync key.
/// </summary>
public sealed class BudgetTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CategoryName { get; init; } = "";
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public string Description { get; init; } = "";
}
