using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Shared.Snapshots;

/// <summary>
/// Pairs the state tax lines of two saved paychecks for side-by-side comparison.
/// The two sides need not levy the same taxes — comparing a New Jersey offer
/// against a Texas one is exactly the point — so this takes the union of both
/// sides' lines and reports a zero where one side has no matching line.
/// <para>
/// Defined once here because MAUI and Blazor each build their own comparison
/// rows and would otherwise drift apart.
/// </para>
/// </summary>
public static class SavedStateTaxLinePairing
{
    /// <summary>One paired line: its label plus the A-side and B-side amounts.</summary>
    public readonly record struct Pair(string Label, decimal AmountA, decimal AmountB);

    /// <summary>
    /// The union of both sides' lines in display order. Falls back to the scalar
    /// state-withholding and assessment fields when neither snapshot carries
    /// itemized lines, so paychecks saved before the itemized model still compare.
    /// </summary>
    public static IReadOnlyList<Pair> Build(SavedPaycheckResultDto a, SavedPaycheckResultDto b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.StateTaxLines.Count == 0 && b.StateTaxLines.Count == 0)
            return LegacyPairs(a, b);

        var pairs = new List<Pair>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in Ordered(a).Concat(Ordered(b)))
        {
            var key = $"{(int)line.Kind}|{line.ShortCode ?? line.Label}";
            if (!seen.Add(key))
                continue;

            pairs.Add(new Pair(line.Label, AmountOf(a, line), AmountOf(b, line)));
        }

        return pairs;
    }

    private static IEnumerable<SavedStateTaxLineDto> Ordered(SavedPaycheckResultDto dto) =>
        dto.StateTaxLines
            .OrderBy(line => (int)line.Kind)
            .ThenByDescending(line => line.Kind == StateTaxLineKind.PayrollAssessment ? line.Amount : 0m)
            .ThenBy(line => line.Label, StringComparer.Ordinal);

    private static decimal AmountOf(SavedPaycheckResultDto dto, SavedStateTaxLineDto line) =>
        dto.StateTaxLines
            .Where(candidate => candidate.Kind == line.Kind
                && string.Equals(
                    candidate.ShortCode ?? candidate.Label,
                    line.ShortCode ?? line.Label,
                    StringComparison.OrdinalIgnoreCase))
            .Sum(candidate => candidate.Amount);

    private static IReadOnlyList<Pair> LegacyPairs(SavedPaycheckResultDto a, SavedPaycheckResultDto b)
    {
        var pairs = new List<Pair>
        {
            new("State Income Tax", a.StateWithholding, b.StateWithholding)
        };

        if (a.StateDisabilityInsurance > 0m || b.StateDisabilityInsurance > 0m)
            pairs.Add(new Pair("State Disability", a.StateDisabilityInsurance, b.StateDisabilityInsurance));

        return pairs;
    }
}
