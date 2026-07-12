namespace PaycheckCalculator.Shared.Budgeting;

/// <summary>Records that the transaction with the given <see cref="Id"/> was deleted at <see cref="DeletedAtUtc"/>.</summary>
public sealed record TransactionTombstone(Guid Id, DateTimeOffset DeletedAtUtc);
