namespace PaycheckCalculator.Core.Explanation;

/// <summary>
/// Aggregate "Show Your Work" record attached to a <see cref="Models.PaycheckResult"/>.
/// Holds one <see cref="LineExplanation"/> per visible paycheck line so the UI
/// can surface a step-by-step modal for any row the user taps an info icon on.
/// </summary>
public sealed class PaycheckExplanation
{
    private readonly Dictionary<ExplanationLineKey, LineExplanation> _byKey;

    public PaycheckExplanation(IReadOnlyList<LineExplanation> lines)
    {
        Lines = lines;
        _byKey = lines.ToDictionary(l => l.Key);
    }

    /// <summary>All line explanations in display order.</summary>
    public IReadOnlyList<LineExplanation> Lines { get; }

    /// <summary>Returns the explanation for <paramref name="key"/>, or <c>null</c> when none was produced (e.g. zero-tax states).</summary>
    public LineExplanation? Get(ExplanationLineKey key)
        => _byKey.TryGetValue(key, out var line) ? line : null;

    /// <summary>An empty explanation, used as a safe default.</summary>
    public static PaycheckExplanation Empty { get; } = new(Array.Empty<LineExplanation>());

    /// <summary>
    /// The authoritative source citations backing this calculation, one per line
    /// that carries a <see cref="LineExplanation.Reference"/>, in display order.
    /// Lines with no governing tax rule (e.g. Gross Pay, Net Pay) are omitted.
    /// Powers the "Accuracy &amp; Sources" view in both front-ends.
    /// </summary>
    public IReadOnlyList<SourceCitation> Sources =>
        Lines.Where(l => !string.IsNullOrEmpty(l.Reference))
             .Select(l => new SourceCitation(l.Title, l.Reference!))
             .ToList();
}
