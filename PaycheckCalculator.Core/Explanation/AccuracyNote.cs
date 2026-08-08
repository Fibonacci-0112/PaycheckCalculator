namespace PaycheckCalculator.Core.Explanation;

/// <summary>A user-facing assumption, approximation, or exclusion attached to a result.</summary>
/// <param name="Title">Short category or heading.</param>
/// <param name="Description">The limitation users need to understand.</param>
public sealed record AccuracyNote(string Title, string Description);
