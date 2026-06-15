using System.Globalization;
using PaycheckCalc.Core.Budgeting;

namespace PaycheckCalc.Blazor.Services.Export;

/// <summary>
/// Renders a <see cref="BudgetReport"/> to PDF bytes.
/// Page 1: Budget-vs-Actual trend table (Month | Budgeted | Actual | Variance).
/// Subsequent pages: per-month spend-by-category breakdowns.
/// Reuses <see cref="PdfDocument"/> and <see cref="PdfLayout"/> from the same assembly.
/// </summary>
public static class BudgetReportPdfRenderer
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

    private static readonly Rgb TextDark = Rgb.Hex("#37474F");
    private static readonly Rgb Red      = Rgb.Hex("#C62828");
    private static readonly Rgb Green    = Rgb.Hex("#2E7D32");
    private static readonly Rgb Gray     = Rgb.Hex("#78909C");

    public static byte[] Render(BudgetReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var doc       = new PdfDocument();
        int catalogId = doc.Reserve();
        int pagesId   = doc.Reserve();
        int fontReg   = doc.Reserve();
        int fontBold  = doc.Reserve();
        var layout    = new PdfLayout(doc, pagesId, fontReg, fontBold);

        var title = $"Budget Report — {Clip(report.BudgetName)}";
        layout.BeginPage(title);
        layout.Subtitle($"Generated {DateTime.Now.ToString("MMMM d, yyyy", Usd)}  |  {report.Months.Count}-month history");

        // ── Budget vs Actual trend ────────────────────────────────────────────
        layout.SectionHeader("Budget vs Actual Trend");
        layout.CompareHeader("Budgeted", "Actual");
        foreach (var pt in report.BudgetVsActual)
        {
            var under = pt.Variance <= 0m;
            var diff  = (under ? "" : "+") + pt.Variance.ToString("C", Usd);
            layout.CompareRow(
                pt.Month.ToString("MMM yyyy", Usd),
                pt.Budgeted.ToString("C", Usd),
                pt.Actual.ToString("C", Usd),
                diff,
                emphasize: false);
        }

        // ── Spend by category (one section per month) ─────────────────────────
        foreach (var month in report.Months)
        {
            layout.SectionHeader($"Spend — {month.ToString("MMMM yyyy", Usd)}");
            var points = report.SpendByCategory
                .Where(p => p.Month == month)
                .OrderBy(p => p.CategoryName, StringComparer.OrdinalIgnoreCase);
            foreach (var pt in points)
            {
                var color = pt.Spent > pt.Budgeted ? Red : TextDark;
                layout.Row(pt.CategoryName, pt.Spent.ToString("C", Usd), color);
            }
        }

        layout.Finish(catalogId);
        return doc.Build(catalogId);
    }

    private static string Clip(string s) => s.Length <= 30 ? s : s.Substring(0, 29) + ".";
}
