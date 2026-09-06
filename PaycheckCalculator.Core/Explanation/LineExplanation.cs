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
/// <param name="Reference">Optional legacy citation retained for compatibility.</param>
/// <param name="SourceRuleIds">Stable IDs of source-manifest rules used by this line.</param>
/// <param name="SubKey">
/// Distinguishes sibling lines that share a <paramref name="Key"/> — a state can
/// levy several payroll assessments (New Jersey withholds both SDI and FLI), and
/// each needs its own breakdown. Null for the single-line case.
/// </param>
public sealed record LineExplanation(
    ExplanationLineKey Key,
    string Title,
    decimal FinalAmount,
    IReadOnlyList<ExplanationStep> Steps,
    string? Reference = null,
    IReadOnlyList<string>? SourceRuleIds = null,
    string? SubKey = null);
