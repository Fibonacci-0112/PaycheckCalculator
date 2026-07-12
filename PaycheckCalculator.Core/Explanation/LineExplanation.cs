namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// Step-by-step breakdown for a single paycheck line item.
/// Built by the calculation engine and consumed by the UI's
/// "Show Your Work" modal.
/// </summary>
/// <param name="Key">Which paycheck line this explains.</param>
/// <param name="Title">Display title for the modal (e.g. "Federal Withholding").</param>
/// <param name="FinalAmount">The amount actually shown on the line, included for the modal heading.</param>
/// <param name="Steps">Ordered list of worksheet-style steps that produced <paramref name="FinalAmount"/>.</param>
/// <param name="Reference">Optional citation, e.g. "IRS Publication 15-T (2026), Worksheet 1A".</param>
public sealed record LineExplanation(
    ExplanationLineKey Key,
    string Title,
    decimal FinalAmount,
    IReadOnlyList<ExplanationStep> Steps,
    string? Reference = null);
