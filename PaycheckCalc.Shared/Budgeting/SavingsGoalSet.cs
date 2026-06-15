namespace PaycheckCalc.Shared.Budgeting;

/// <summary>The complete savings-goal state for one user/device: live goals plus delete tombstones.</summary>
public sealed record SavingsGoalSet(
    IReadOnlyList<SavingsGoalDto> Goals,
    IReadOnlyList<SavingsGoalTombstone> Tombstones)
{
    public static SavingsGoalSet Empty { get; } = new([], []);
}
