using System.Globalization;
using System.Text;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Csv;

/// <summary>
/// Renders the per-period paycheck results as RFC 4180 CSV text. The rows mirror
/// the sections shown on the Results page (and in the PDF export): Income, Taxes,
/// Deductions, and a Summary. Money is written as a plain decimal (no currency
/// symbol) in the invariant culture so it imports cleanly as a number into
/// spreadsheets; the only non-numeric value is the optional state name.
/// </summary>
internal static class PaycheckCsvRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly char[] MustQuote = { ',', '"', '\r', '\n' };

    /// <summary>Renders the per-period results to a CSV document.</summary>
    public static string Render(ResultCardModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        Line(sb, "Section", "Item", "Value");

        if (!string.IsNullOrEmpty(result.StateName))
            Line(sb, "Summary", "State", result.StateName);

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

        return sb.ToString();
    }

    private static void Money(StringBuilder sb, string section, string item, decimal amount) =>
        Line(sb, section, item, amount.ToString("0.00", Invariant));

    private static void Line(StringBuilder sb, string section, string item, string value) =>
        sb.Append(Escape(section)).Append(',')
          .Append(Escape(item)).Append(',')
          .Append(Escape(value)).Append("\r\n");

    /// <summary>Quotes a field per RFC 4180 when it contains a comma, quote, CR, or LF.</summary>
    private static string Escape(string field) =>
        field.IndexOfAny(MustQuote) < 0
            ? field
            : "\"" + field.Replace("\"", "\"\"") + "\"";
}
