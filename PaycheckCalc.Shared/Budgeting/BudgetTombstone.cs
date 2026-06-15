namespace PaycheckCalc.Shared.Budgeting;

/// <summary>Records that a budget with the given <see cref="Name"/> was deleted at <see cref="DeletedAtUtc"/>.</summary>
public sealed record BudgetTombstone(string Name, DateTimeOffset DeletedAtUtc);
