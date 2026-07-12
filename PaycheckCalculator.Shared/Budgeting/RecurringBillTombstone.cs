namespace PaycheckCalculator.Shared.Budgeting;

/// <summary>Records that the recurring bill with the given <see cref="Id"/> was deleted at <see cref="DeletedAtUtc"/>.</summary>
public sealed record RecurringBillTombstone(Guid Id, DateTimeOffset DeletedAtUtc);
