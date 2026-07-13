namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// A single accuracy citation — the human-readable label of a calculation step and the
/// source document that defines it (e.g. "IRS Publication 15-T (2026), Worksheet 1A").
/// Aggregated from <see cref="LineExplanation.Reference"/> values across all lines in a
/// <see cref="PaycheckExplanation"/> and surfaced in the Accuracy &amp; Sources screen.
/// </summary>
/// <param name="Label">The display name of the calculation step this citation belongs to (e.g. "Federal Withholding").</param>
/// <param name="Reference">The authoritative source document reference (e.g. "IRS Publication 15-T (2026), Worksheet 1A").</param>
public sealed record SourceCitation(string Label, string Reference);
