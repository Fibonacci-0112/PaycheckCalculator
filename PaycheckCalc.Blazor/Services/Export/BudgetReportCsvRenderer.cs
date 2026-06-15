using System.Globalization;
using System.Text;
using PaycheckCalc.Core.Budgeting;

namespace PaycheckCalc.Blazor.Services.Export;

/// <summary>
/// Renders a <see cref="BudgetReport"/> as RFC 4180 CSV text.
/// Table 1: categories as rows, months as columns, spend values as decimals.
/// Table 2: budget-vs-actual by month.
/// </summary>
public static class BudgetReportCsvRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly char[] MustQuote = [',', '"', '\r', '\n'];

    public static string Render(BudgetReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        // ── Table 1: Spend by Category ──────────────────────────────────────────
        // Header: Category, Type, <Month1>, <Month2>, ..., Total
        var header = new List<string>(report.Months.Count + 3) { "Category", "Type" };
        foreach (var m in report.Months)
            header.Add(m.ToString("MMM yyyy", Invariant));
        header.Add("Total");
        LineN(sb, header);

        var byCategory = report.SpendByCategory
            .GroupBy(p => p.CategoryName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byCategory)
        {
            var byMonth = group.ToDictionary(p => p.Month);
            var first   = group.First();
            var row     = new List<string>(report.Months.Count + 3) { first.CategoryName, first.BudgetType.ToString() };
            decimal total = 0m;
            foreach (var m in report.Months)
            {
                var spent = byMonth.TryGetValue(m, out var pt) ? pt.Spent : 0m;
                total += spent;
                row.Add(Dec(spent));
            }
            row.Add(Dec(total));
            LineN(sb, row);
        }

        sb.Append("\r\n");

        // ── Table 2: Budget vs Actual ───────────────────────────────────────────
        LineN(sb, ["Month", "Budgeted", "Actual", "Variance"]);
        foreach (var pt in report.BudgetVsActual)
        {
            LineN(sb,
            [
                pt.Month.ToString("MMM yyyy", Invariant),
                Dec(pt.Budgeted),
                Dec(pt.Actual),
                Dec(pt.Variance),
            ]);
        }

        return sb.ToString();
    }

    private static string Dec(decimal v) => v.ToString("0.00", Invariant);

    private static void LineN(StringBuilder sb, IEnumerable<string> cols)
    {
        bool first = true;
        foreach (var col in cols)
        {
            if (!first) sb.Append(',');
            sb.Append(Escape(col));
            first = false;
        }
        sb.Append("\r\n");
    }

    private static string Escape(string field) =>
        field.IndexOfAny(MustQuote) < 0
            ? field
            : "\"" + field.Replace("\"", "\"\"") + "\"";
}
