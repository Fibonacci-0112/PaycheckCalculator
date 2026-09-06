namespace PaycheckCalculator.Core.Explanation;

using PaycheckCalculator.Core.Tax.Sources;

/// <summary>
/// Aggregate "Show Your Work" record attached to a <see cref="Models.PaycheckResult"/>.
/// Holds one <see cref="LineExplanation"/> per visible paycheck line so the UI
/// can surface a step-by-step modal for any row the user taps an info icon on.
/// </summary>
public sealed class PaycheckExplanation
{
    private readonly Dictionary<(ExplanationLineKey Key, string SubKey), LineExplanation> _byKey;

    public PaycheckExplanation(IReadOnlyList<LineExplanation> lines, TaxSourceCatalog? sourceCatalog = null)
    {
        Lines = lines;
        // A state can emit several lines of the same kind (New Jersey withholds
        // both SDI and FLI), so the lookup is keyed by line *and* sub-key. First
        // one wins rather than throwing, since a duplicate is a display concern
        // and must never break a calculation.
        _byKey = new Dictionary<(ExplanationLineKey, string), LineExplanation>();
        foreach (var line in lines)
            _byKey.TryAdd((line.Key, line.SubKey ?? string.Empty), line);

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

    /// <summary>
    /// Returns the explanation for <paramref name="key"/>, or <c>null</c> when none
    /// was produced (e.g. zero-tax states). When several lines share the key, the
    /// first in display order wins.
    /// </summary>
    public LineExplanation? Get(ExplanationLineKey key)
        => _byKey.TryGetValue((key, string.Empty), out var line)
            ? line
            : Lines.FirstOrDefault(l => l.Key == key);

    /// <summary>
    /// Returns the explanation for a specific sibling line, e.g. New Jersey's FLI
    /// as distinct from its SDI. Falls back to the keyed lookup when
    /// <paramref name="subKey"/> is null.
    /// </summary>
    public LineExplanation? Get(ExplanationLineKey key, string? subKey)
        => subKey is null
            ? Get(key)
            : _byKey.TryGetValue((key, subKey), out var line) ? line : null;

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
