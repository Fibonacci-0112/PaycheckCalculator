namespace PaycheckCalculator.Core.Explanation;

using PaycheckCalculator.Core.Tax.Sources;

/// <summary>
/// Aggregate "Show Your Work" record attached to a <see cref="Models.PaycheckResult"/>.
/// Holds one <see cref="LineExplanation"/> per visible paycheck line so the UI
/// can surface a step-by-step modal for any row the user taps an info icon on.
/// </summary>
public sealed class PaycheckExplanation
{
    private readonly Dictionary<ExplanationLineKey, LineExplanation> _byKey;

    public PaycheckExplanation(IReadOnlyList<LineExplanation> lines, TaxSourceCatalog? sourceCatalog = null)
    {
        Lines = lines;
        _byKey = lines.ToDictionary(l => l.Key);

        Sources = lines
            .SelectMany(line => ResolveSources(line, sourceCatalog))
            .GroupBy(source => source.RuleId ?? source.Reference, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        AccuracyNotes = Sources
            .SelectMany(source =>
                source.Approximations.Select(note => new AccuracyNote("Approximation", note))
                    .Concat(source.Exclusions.Select(note => new AccuracyNote("Exclusion", note))))
            .Distinct()
            .ToList();
    }

    /// <summary>All line explanations in display order.</summary>
    public IReadOnlyList<LineExplanation> Lines { get; }

    /// <summary>
    /// Unique source citations aggregated from all <see cref="LineExplanation.Reference"/>
    /// values in <see cref="Lines"/>. Used by the Accuracy &amp; Sources screen and export
    /// sections. Empty when no lines carry a reference.
    /// </summary>
    public IReadOnlyList<SourceCitation> Sources { get; }

    /// <summary>Manifest approximations and exclusions for the rules used by this result.</summary>
    public IReadOnlyList<AccuracyNote> AccuracyNotes { get; }

    /// <summary>Returns the explanation for <paramref name="key"/>, or <c>null</c> when none was produced (e.g. zero-tax states).</summary>
    public LineExplanation? Get(ExplanationLineKey key)
        => _byKey.TryGetValue(key, out var line) ? line : null;

    /// <summary>An empty explanation, used as a safe default.</summary>
    public static PaycheckExplanation Empty { get; } = new(Array.Empty<LineExplanation>());

    private static IEnumerable<SourceCitation> ResolveSources(
        LineExplanation line,
        TaxSourceCatalog? sourceCatalog)
    {
        if (sourceCatalog is not null && line.SourceRuleIds is { Count: > 0 })
        {
            foreach (var id in line.SourceRuleIds)
                yield return sourceCatalog.CreateCitation(line.Title, sourceCatalog.GetById(id));
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(line.Reference))
            yield return new SourceCitation(line.Title, line.Reference);
    }
}
