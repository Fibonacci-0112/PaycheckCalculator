namespace PaycheckCalc.Shared.Budgeting;

/// <summary>The complete budget state for one user/device: live budgets plus delete tombstones.</summary>
public sealed record BudgetSet(
    IReadOnlyList<BudgetDto> Budgets,
    IReadOnlyList<BudgetTombstone> Tombstones)
{
    public static BudgetSet Empty { get; } = new([], []);
}
