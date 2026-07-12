namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// One step in a tax-line "Show Your Work" breakdown.
/// Mirrors a single line on the IRS / state worksheet so the user
/// can follow how each intermediate value was reached.
/// </summary>
/// <param name="Label">Short heading for the step, e.g. "Annualized wages".</param>
/// <param name="Detail">Human-readable description of what the step does and why.</param>
/// <param name="Value">Computed value at this step (e.g. an intermediate dollar amount). Null when the step is purely informational.</param>
/// <param name="Formula">Optional formula or rule reference shown verbatim (e.g. "$5,000 × 26 = $130,000").</param>
public sealed record ExplanationStep(
    string Label,
    string Detail,
    decimal? Value = null,
    string? Formula = null);
