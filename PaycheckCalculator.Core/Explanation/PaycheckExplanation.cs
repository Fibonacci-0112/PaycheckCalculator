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
}
