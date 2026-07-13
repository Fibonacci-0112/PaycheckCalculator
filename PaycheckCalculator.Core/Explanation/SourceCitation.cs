namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// One authoritative tax-rule citation backing a paycheck line, surfaced on the
/// "Accuracy &amp; Sources" view. Derived from the same <see cref="LineExplanation.Reference"/>
/// text already shown in each line's "Show Your Work" modal — this type simply
/// aggregates those citations into a single, consolidated list.
/// </summary>
/// <param name="Title">Display title of the line this citation backs (e.g. "Federal Withholding").</param>
/// <param name="Citation">The authoritative reference text (e.g. "IRS Publication 15-T (2026)...").</param>
public sealed record SourceCitation(string Title, string Citation);
