using PaycheckCalculator.Blazor.Models;
using PaycheckCalculator.Shared.Snapshots;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Builds the side-by-side comparison rows for two saved paychecks, mirroring the
/// MAUI app's Paychecks-page comparison. Operates on the stored
/// <see cref="SavedPaycheckResultDto"/> numbers, so no recomputation is needed.
/// </summary>
public static class PaycheckComparison
{
    /// <summary>
    /// Per-metric comparison of two saved results (A vs B). State tax lines are
    /// itemized from both sides' snapshots; deduction rows appear only when either
    /// side is non-zero; the Net Pay row is flagged for emphasis. Medicare folds in
    /// any Additional Medicare.
    /// </summary>
    public static IReadOnlyList<ComparisonRow> BuildRows(SavedPaycheckResultDto a, SavedPaycheckResultDto b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var rows = new List<ComparisonRow>
        {
            new("Gross Pay", a.GrossPay, b.GrossPay),
            new("Federal Tax", a.FederalWithholding, b.FederalWithholding),
            new("Social Security", a.SocialSecurityWithholding, b.SocialSecurityWithholding),
            new("Medicare",
                a.MedicareWithholding + a.AdditionalMedicareWithholding,
                b.MedicareWithholding + b.AdditionalMedicareWithholding),
        };

        // One row per state line — a New Jersey paycheck compares its SDI and FLI
        // against whatever the other side levies, or against zero.
        foreach (var pair in SavedStateTaxLinePairing.Build(a, b))
            rows.Add(new ComparisonRow(pair.Label, pair.AmountA, pair.AmountB));

        if (a.PreTaxDeductions > 0m || b.PreTaxDeductions > 0m)
            rows.Add(new ComparisonRow("Pre-Tax Deductions", a.PreTaxDeductions, b.PreTaxDeductions));

        if (a.PostTaxDeductions > 0m || b.PostTaxDeductions > 0m)
            rows.Add(new ComparisonRow("Post-Tax Deductions", a.PostTaxDeductions, b.PostTaxDeductions));

        rows.Add(new ComparisonRow("Total Taxes", a.TotalTaxes, b.TotalTaxes));
        rows.Add(new ComparisonRow("Net Pay", a.NetPay, b.NetPay, highlight: true));

        return rows;
    }
}
