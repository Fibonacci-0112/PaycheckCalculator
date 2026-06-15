namespace PaycheckCalc.Shared.Budgeting;

/// <summary>The complete transaction state for one user/device: live transactions plus delete tombstones.</summary>
public sealed record TransactionSet(
    IReadOnlyList<TransactionDto> Transactions,
    IReadOnlyList<TransactionTombstone> Tombstones)
{
    public static TransactionSet Empty { get; } = new([], []);
}
