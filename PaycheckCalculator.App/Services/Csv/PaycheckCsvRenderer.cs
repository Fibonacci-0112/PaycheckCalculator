using System.Globalization;
using System.Text;
using PaycheckCalculator.App.Models;
using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.App.Services.Csv;

/// <summary>
/// Renders the paycheck results as RFC 4180 CSV text. The per-period rows mirror
/// the Results page (Income, Taxes, Deductions, Summary); an optional
/// <see cref="AnnualProjectionModel"/> and an optional A/B <see cref="ComparisonRow"/>
/// table are appended when supplied. Money is written as a plain decimal (no
/// currency symbol) in the invariant culture so it imports cleanly as a number;
/// the only non-numeric values are the state and comparison labels.
/// </summary>
internal static class PaycheckCsvRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly char[] MustQuote = { ',', '"', '\r', '\n' };

    /// <summary>Renders the per-period results — plus optional annual projection and A/B comparison — to CSV.</summary>
    public static string Render(ResultCardModel result,
        AnnualProjectionModel? annual = null,
        IReadOnlyList<ComparisonRow>? comparison = null,
        string comparisonNameA = "Paycheck A", string comparisonNameB = "Paycheck B")
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        Line(sb, "Section", "Item", "Value");

        if (!string.IsNullOrEmpty(result.StateName))
            Line(sb, "Summary", "State", result.StateName);
        Line(sb, "Summary", "Tax Year", result.TaxYear.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Money(sb, "Income", "Gross Pay", result.GrossPay);
        Money(sb, "Income", "Federal Taxable Income", result.FederalTaxableIncome);
        Money(sb, "Income", "FICA Taxable Income", result.FicaTaxableWages);
        Money(sb, "Income", "State Taxable Income", result.StateTaxableWages);

        Money(sb, "Taxes", "Federal Tax", result.FederalWithholding);
        Money(sb, "Taxes", "Social Security Tax", result.SocialSecurityWithholding);
        Money(sb, "Taxes", "Medicare Tax", result.MedicareWithholding + result.AdditionalMedicareWithholding);
        Money(sb, "Taxes", "State Income Tax", result.StateWithholding);
        if (result.StateDisabilityInsurance > 0m)
            Money(sb, "Taxes", result.StateDisabilityInsuranceLabel, result.StateDisabilityInsurance);

        if (result.PreTaxDeductions > 0m)
            Money(sb, "Deductions", "Pre-Tax Deductions", result.PreTaxDeductions);
        if (result.PostTaxDeductions > 0m)
            Money(sb, "Deductions", "Post-Tax Deductions", result.PostTaxDeductions);

        Money(sb, "Summary", "Total Taxes", result.TotalTaxes);
        Money(sb, "Summary", "Net Pay", result.NetPay);

        foreach (var source in result.Explanation.Sources)
            Line(sb, "Sources", source.Label, source.Reference);

        if (annual is not null)
            WriteAnnual(sb, annual);
        if (comparison is { Count: > 0 })
            WriteComparison(sb, comparison, comparisonNameA, comparisonNameB);

        var sources = result.Explanation.Sources;
        if (sources.Count > 0)
            WriteSources(sb, sources);

        return sb.ToString();
    }

    /// <summary>Renders a standalone A/B comparison table to CSV.</summary>
    public static string RenderComparison(IReadOnlyList<ComparisonRow> comparison, string nameA, string nameB)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var sb = new StringBuilder();
        Line4(sb, "Metric", nameA, nameB, "Difference");
        foreach (var r in comparison)
            Line4(sb, r.Label, Dec(r.ValueA), Dec(r.ValueB), Dec(r.Difference));
        return sb.ToString();
    }

    private static void WriteAnnual(StringBuilder sb, AnnualProjectionModel a)
    {
        Money(sb, "Annualized", "Gross Pay", a.AnnualizedGrossPay);
        if (a.AnnualizedPreTaxDeductions > 0m)
            Money(sb, "Annualized", "Pre-Tax Deductions", a.AnnualizedPreTaxDeductions);
        Money(sb, "Annualized", "Federal Withholding", a.AnnualizedFederalWithholding);
        Money(sb, "Annualized", "State Withholding", a.AnnualizedStateWithholding);
        Money(sb, "Annualized", "FICA", a.AnnualizedFica);
        Money(sb, "Annualized", "Net Pay", a.AnnualizedNetPay);

        Line(sb, "Projected YTD", "Paycheck Number", a.CurrentPaycheckNumber.ToString(Invariant));
        Line(sb, "Projected YTD", "Pay Periods Per Year", a.PayPeriodsPerYear.ToString(Invariant));
        Line(sb, "Projected YTD", "Remaining Paychecks", a.RemainingPaychecks.ToString(Invariant));
        Money(sb, "Projected YTD", "Gross Pay", a.ProjectedYtdGrossPay);
        Money(sb, "Projected YTD", "Federal Withholding", a.ProjectedYtdFederalWithholding);
        Money(sb, "Projected YTD", "State Withholding", a.ProjectedYtdStateWithholding);
        Money(sb, "Projected YTD", "FICA", a.ProjectedYtdFica);
        Money(sb, "Projected YTD", "Net Pay", a.ProjectedYtdNetPay);

        Money(sb, "Year-End Estimate", "Est. Annual Federal Liability", a.EstimatedAnnualFederalLiability);
        Money(sb, "Year-End Estimate", "Est. Annual FICA Liability", a.EstimatedAnnualFicaLiability);
        Money(sb, "Year-End Estimate", "Estimated Total Liability", a.EstimatedTotalLiability);
        Money(sb, "Year-End Estimate", "Annualized Total Withholding", a.AnnualizedTotalWithholding);
        Money(sb, "Year-End Estimate", "Over/Under Withholding", a.OverUnderWithholding);
    }

    private static void WriteComparison(StringBuilder sb, IReadOnlyList<ComparisonRow> rows, string nameA, string nameB)
    {
        sb.Append("\r\n"); // blank separator before the second table
        Line4(sb, "Metric", nameA, nameB, "Difference");
        foreach (var r in rows)
            Line4(sb, r.Label, Dec(r.ValueA), Dec(r.ValueB), Dec(r.Difference));
    }

    private static void WriteSources(StringBuilder sb, IReadOnlyList<SourceCitation> sources)
    {
        sb.Append("\r\n"); // blank separator before the sources table
        Line(sb, "Section", "Calculation Step", "Source");
        foreach (var s in sources)
            Line(sb, "Sources", s.Label, s.Reference);
    }

    private static string Dec(decimal value) => value.ToString("0.00", Invariant);

    private static void Money(StringBuilder sb, string section, string item, decimal amount) =>
        Line(sb, section, item, amount.ToString("0.00", Invariant));

    private static void Line(StringBuilder sb, string section, string item, string value) =>
        sb.Append(Escape(section)).Append(',')
          .Append(Escape(item)).Append(',')
          .Append(Escape(value)).Append("\r\n");

    private static void Line4(StringBuilder sb, string c1, string c2, string c3, string c4) =>
        sb.Append(Escape(c1)).Append(',')
          .Append(Escape(c2)).Append(',')
          .Append(Escape(c3)).Append(',')
          .Append(Escape(c4)).Append("\r\n");

    /// <summary>Quotes a field per RFC 4180 when it contains a comma, quote, CR, or LF.</summary>
    private static string Escape(string field) =>
        field.IndexOfAny(MustQuote) < 0
            ? field
            : "\"" + field.Replace("\"", "\"\"") + "\"";
}
