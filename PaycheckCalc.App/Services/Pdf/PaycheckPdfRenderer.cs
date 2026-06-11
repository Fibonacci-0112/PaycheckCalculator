using System.Globalization;
using System.Text;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <summary>
/// Lays out the per-period paycheck results onto a single Letter-size page and
/// produces the PDF bytes. Pure presentation: it consumes the same
/// <see cref="ResultCardModel"/> the Results page binds to. The page shows an
/// INCOME section, a TAXES section, and the resulting net pay.
/// </summary>
internal static class PaycheckPdfRenderer
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

    // Colors mirror the Results page palette.
    private static readonly Rgb TextDark = Rgb.Hex("#37474F");
    private static readonly Rgb Red = Rgb.Hex("#C62828");
    private static readonly Rgb Green = Rgb.Hex("#2E7D32");

    /// <summary>Renders the per-period results to single-page PDF bytes.</summary>
    public static byte[] Render(ResultCardModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var doc = new PdfDocument();
        int catalogId = doc.Reserve();
        int pagesId = doc.Reserve();
        int helvetica = doc.Reserve();
        int helveticaBold = doc.Reserve();

        var layout = new PdfLayout(doc, pagesId, helvetica, helveticaBold);

        var title = string.IsNullOrEmpty(result.StateName)
            ? "Paycheck Summary"
            : $"Paycheck Summary - {result.StateName}";
        layout.BeginPage(title);
        // Note: the content stream is written as ASCII (see PdfLayout.Finish), so
        // keep page text within ASCII — use a plain "|" separator, not a bullet.
        layout.Subtitle($"Generated {DateTime.Now.ToString("MMMM d, yyyy", Usd)}  |  2026 tax tables");

        layout.SectionHeader("Income");
        layout.Row("Gross Pay", Money(result.GrossPay), TextDark, bold: true);
        layout.Row("Federal Taxable Income", Money(result.FederalTaxableIncome), TextDark);
        layout.Row("FICA Taxable Income", Money(result.FicaTaxableWages), TextDark);
        layout.Row("State Taxable Income", Money(result.StateTaxableWages), TextDark);

        layout.SectionHeader("Taxes");
        layout.Row("Federal Tax", Money(result.FederalWithholding), Red);
        layout.Row("Social Security Tax", Money(result.SocialSecurityWithholding), Red);
        layout.Row("Medicare Tax", Money(result.MedicareWithholding + result.AdditionalMedicareWithholding), Red);
        layout.Row("State Income Tax", Money(result.StateWithholding), Red);

        layout.Banner("NET PAY", Money(result.NetPay), Green);

        layout.Finish(catalogId);
        return doc.Build(catalogId);
    }

    private static string Money(decimal value) => value.ToString("C", Usd);
}

/// <summary>RGB color with components in the 0..1 range expected by PDF operators.</summary>
internal readonly struct Rgb(double r, double g, double b)
{
    public readonly double R = r;
    public readonly double G = g;
    public readonly double B = b;

    public static Rgb Hex(string hex) => new(
        Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0,
        Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0,
        Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0);
}

/// <summary>
/// A simple top-down flow layout over one or more Letter pages. Tracks a
/// vertical cursor (in PDF user space, origin bottom-left) and starts a new
/// page automatically when content would overflow the bottom margin.
/// </summary>
internal sealed class PdfLayout
{
    private const double PageWidth = 612;   // 8.5in * 72
    private const double PageHeight = 792;  // 11in  * 72
    private const double Margin = 54;
    private const double ContentLeft = Margin;
    private const double ContentRight = PageWidth - Margin;
    private const double ContentWidth = ContentRight - ContentLeft;

    private static readonly Rgb HeaderBlue = Rgb.Hex("#1565C0");
    private static readonly Rgb Gray = Rgb.Hex("#78909C");
    private static readonly Rgb Divider = Rgb.Hex("#E8EAED");
    private static readonly Rgb White = new(1, 1, 1);
    private static readonly Rgb Faint = Rgb.Hex("#B0BEC5");
    private static readonly Rgb LabelDark = Rgb.Hex("#37474F");

    private sealed class Page
    {
        public readonly StringBuilder Content = new();
    }

    private readonly PdfDocument _doc;
    private readonly int _pagesId;
    private readonly int _fontRegular;
    private readonly int _fontBold;
    private readonly List<Page> _pages = new();

    private Page _page = null!;
    private double _y;
    private string _currentTitle = "";

    public PdfLayout(PdfDocument doc, int pagesId, int fontRegular, int fontBold)
    {
        _doc = doc;
        _pagesId = pagesId;
        _fontRegular = fontRegular;
        _fontBold = fontBold;
    }

    public void BeginPage(string title)
    {
        _currentTitle = title;
        _page = new Page();
        _pages.Add(_page);

        // Top banner.
        FillRect(0, PageHeight - 64, PageWidth, 64, HeaderBlue);
        Text(ContentLeft, PageHeight - 42, title, bold: true, size: 18, color: White);

        _y = PageHeight - 64 - 26;
    }

    public void Subtitle(string text)
    {
        Text(ContentLeft, _y, text, bold: false, size: 9.5, color: Faint);
        _y -= 22;
    }

    public void SectionHeader(string text)
    {
        EnsureSpace(28);
        _y -= 6;
        Text(ContentLeft, _y, text.ToUpperInvariant(), bold: true, size: 10.5, color: Gray);
        _y -= 16;
    }

    public void Row(string label, string value, Rgb valueColor, bool bold = false)
    {
        const double rowHeight = 21;
        EnsureSpace(rowHeight);
        double baseline = _y - 14;
        Text(ContentLeft, baseline, label, bold: false, size: 11, color: LabelDark);
        double w = MeasureNumeric(value, 11);
        Text(ContentRight - w, baseline, value, bold: bold, size: 11, color: valueColor);
        _y -= rowHeight;
        FillRect(ContentLeft, _y + 4, ContentWidth, 0.6, Divider);
    }

    public void Banner(string label, string value, Rgb background)
    {
        const double height = 58;
        EnsureSpace(height + 14);
        _y -= 8;
        FillRect(ContentLeft, _y - height, ContentWidth, height, background);

        double labelWidth = MeasureApprox(label, 11);
        Text(ContentLeft + (ContentWidth - labelWidth) / 2, _y - 22, label, bold: true, size: 11, color: White);

        double valueWidth = MeasureNumeric(value, 26);
        Text(ContentLeft + (ContentWidth - valueWidth) / 2, _y - 50, value, bold: true, size: 26, color: White);

        _y -= height + 14;
    }

    /// <summary>Emits the font, content-stream, and page objects and wires up the pages tree + catalog.</summary>
    public void Finish(int catalogId)
    {
        _doc.Set(_fontRegular, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        _doc.Set(_fontBold, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        var kids = new List<int>(_pages.Count);
        foreach (var page in _pages)
        {
            int contentId = _doc.Add(PdfDocument.Stream("", Encoding.ASCII.GetBytes(page.Content.ToString())));

            int pageId = _doc.Add(
                $"<< /Type /Page /Parent {_pagesId} 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                $"/Resources << /Font << /F1 {_fontRegular} 0 R /F2 {_fontBold} 0 R >> >> /Contents {contentId} 0 R >>");
            kids.Add(pageId);
        }

        var kidRefs = string.Join(" ", kids.Select(k => $"{k} 0 R"));
        _doc.Set(_pagesId, $"<< /Type /Pages /Kids [ {kidRefs} ] /Count {kids.Count} >>");
        _doc.Set(catalogId, $"<< /Type /Catalog /Pages {_pagesId} 0 R >>");
    }

    // ── Primitive content emitters ──────────────────────────────

    private void EnsureSpace(double height)
    {
        if (_y - height < Margin)
            BeginPage($"{_currentTitle} (continued)");
    }

    private void FillRect(double x, double y, double w, double h, Rgb color)
    {
        _page.Content
            .Append($"{Col(color)} rg\n")
            .Append($"{Num(x)} {Num(y)} {Num(w)} {Num(h)} re\n")
            .Append("f\n");
    }

    private void Text(double x, double y, string text, bool bold, double size, Rgb color)
    {
        _page.Content
            .Append("BT\n")
            .Append($"/{(bold ? "F2" : "F1")} {Num(size)} Tf\n")
            .Append($"{Col(color)} rg\n")
            .Append($"{Num(x)} {Num(y)} Td\n")
            .Append($"({Escape(text)}) Tj\n")
            .Append("ET\n");
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Col(Rgb c) =>
        $"{Num(c.R)} {Num(c.G)} {Num(c.B)}";

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    /// <summary>Accurate Helvetica/Helvetica-Bold width for the glyphs used in currency values.</summary>
    private static double MeasureNumeric(string text, double size)
    {
        double units = 0;
        foreach (char c in text)
            units += c switch
            {
                ' ' => 278,
                '(' or ')' or '-' => 333,
                ',' or '.' => 278,
                '$' => 556,
                >= '0' and <= '9' => 556,
                _ => 556,
            };
        return units / 1000.0 * size;
    }

    /// <summary>Rough proportional width estimate, used only for centering short labels.</summary>
    private static double MeasureApprox(string text, double size) => text.Length * 0.55 * size;
}
