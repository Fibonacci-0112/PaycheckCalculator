namespace PaycheckCalculator.Shared.Budgeting;

/// <summary>The complete recurring-bill state for one user/device: live bills plus delete tombstones.</summary>
public sealed record RecurringBillSet(
    IReadOnlyList<RecurringBillDto> Bills,
    IReadOnlyList<RecurringBillTombstone> Tombstones)
{
    public static RecurringBillSet Empty { get; } = new([], []);
}
