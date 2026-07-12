namespace PaycheckCalculator.Shared.Budgeting;

/// <summary>Records that the savings goal with the given <see cref="Id"/> was deleted at <see cref="DeletedAtUtc"/>.</summary>
public sealed record SavingsGoalTombstone(Guid Id, DateTimeOffset DeletedAtUtc);
