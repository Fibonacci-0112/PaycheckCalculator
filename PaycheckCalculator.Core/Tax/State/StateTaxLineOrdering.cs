namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// The single definition of the order state tax lines appear in, everywhere:
/// results pages, "Show Your Work" line lists, CSV/PDF exports and A/B
/// comparison rows. Nothing re-sorts these locally.
/// <para>
/// Income taxes run broadest-to-narrowest — state, then county, then local —
/// mirroring how a paystub reads. Payroll assessments follow, largest first, so
/// the deduction the employee actually notices leads.
/// </para>
/// </summary>
public static class StateTaxLineOrdering
{
    /// <summary>
    /// Orders <paramref name="lines"/> for display and drops zero-amount lines,
    /// which a paycheck should never show. Ties break on label so the order is
    /// stable across recalculations.
    /// </summary>
    public static IReadOnlyList<StateTaxLine> Order(IEnumerable<StateTaxLine>? lines)
    {
        if (lines is null)
            return Array.Empty<StateTaxLine>();

        return lines
            .Where(line => line.Amount != 0m)
            .OrderBy(line => (int)line.Kind)
            .ThenByDescending(line => line.Kind == StateTaxLineKind.PayrollAssessment ? line.Amount : 0m)
            .ThenBy(line => line.Label, StringComparer.Ordinal)
            .ToList();
    }
}
