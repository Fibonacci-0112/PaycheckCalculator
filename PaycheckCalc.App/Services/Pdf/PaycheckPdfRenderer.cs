using System.Globalization;
using System.Text;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <summary>
/// Lays out the paycheck results and annual projection (plus the doughnut
/// chart image) onto Letter-size pages and produces the PDF bytes. Pure
/// presentation: it consumes the same <see cref="ResultCardModel"/> /
/// <see cref="AnnualProjectionModel"/> the Results page binds to.
/// </summary>
internal static class PaycheckPdfRenderer
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

    // Colors mirror the Results page palette.
    private static readonly Rgb TextDark = Rgb.Hex("#37474F");
    private static readonly Rgb Red = Rgb.Hex("#C62828");
    private static readonly Rgb Green = Rgb.Hex("#2E7D32");
    private static readonly Rgb Brown = Rgb.Hex("#5D4037");

    /// <summary>Renders the export to PDF. <paramref name="chart"/> is optional.</summary>
    public static byte[] Render(
        ResultCardModel result,
        AnnualProjectionModel? projection,
        (byte[] Jpeg, int Width, int Height)? chart)
    {
        var doc = new PdfDocument();
        int catalogId = doc.Reserve();
        int pagesId = doc.Reserve();
        int helvetica = doc.Reserve();
        int helveticaBold = doc.Reserve();

        var layout = new PdfLayout(doc, pagesId, helvetica, helveticaBold);

        // ── Page 1: per-period results ──────────────────────────
        var title = string.IsNullOrEmpty(result.StateName)
            ? "Paycheck Summary"
            : $"Paycheck Summary — {result.StateName}";
        layout.BeginPage(title);
        layout.Subtitle($"Generated {DateTime.Now.ToString("MMMM d, yyyy", Usd)}  •  2026 tax tables");

        layout.SectionHeader("Income");
        layout.Row("Gross Pay", Money(result.GrossPay), TextDark, bold: true);
        layout.Row("Federal Taxable Income", Money(result.FederalTaxableIncome), TextDark);
        layout.Row("FICA Taxable Income", Money(result.FicaTaxableWages), TextDark);
        layout.Row("State Taxable Income", Money(result.StateTaxableWages), TextDark);

        layout.SectionHeader("Tax Withholdings");
        layout.Row("Federal Tax", Money(result.FederalWithholding), Red);
        layout.Row("Social Security Tax", Money(result.SocialSecurityWithholding), Red);
        layout.Row("Medicare Tax", Money(result.MedicareWithholding + result.AdditionalMedicareWithholding), Red);
        layout.Row("State Income Tax", Money(result.StateWithholding), Red);
        if (result.StateDisabilityInsurance > 0)
            layout.Row(result.StateDisabilityInsuranceLabel, Money(result.StateDisabilityInsurance), Red);

        if (result.PreTaxDeductions > 0 || result.PostTaxDeductions > 0)
        {
            layout.SectionHeader("Deductions");
            if (result.PreTaxDeductions > 0)
                layout.Row("Pre-Tax Deductions", Money(result.PreTaxDeductions), Brown);
            if (result.PostTaxDeductions > 0)
                layout.Row("Post-Tax Deductions", Money(result.PostTaxDeductions), Brown);
        }

        layout.Banner("NET PAY", Money(result.NetPay), Green);

        if (chart is { } c)
        {
            layout.SectionHeader("Where Your Money Goes");
            layout.Image(c.Jpeg, c.Width, c.Height);
        }

        // ── Page 2: annual projection ───────────────────────────
        if (projection is { } p)
        {
            layout.BeginPage("Annual Projection");
            layout.Subtitle(
                $"Based on {p.PayPeriodsPerYear} pay periods  •  paycheck #{p.CurrentPaycheckNumber} of {p.PayPeriodsPerYear}");

            layout.SectionHeader("Annualized Amounts");
            layout.Row("Gross Pay", Money(p.AnnualizedGrossPay), TextDark, bold: true);
            if (p.AnnualizedPreTaxDeductions > 0)
                layout.Row("Pre-Tax Deductions", Money(p.AnnualizedPreTaxDeductions), Brown);
            if (p.AnnualizedPostTaxDeductions > 0)
                layout.Row("Post-Tax Deductions", Money(p.AnnualizedPostTaxDeductions), Brown);
            layout.Row("Federal Taxable Wages", Money(p.AnnualizedFederalTaxableWages), TextDark);
            layout.Row("FICA Taxable Wages", Money(p.AnnualizedFicaTaxableWages), TextDark);
            layout.Row("State Taxable Wages", Money(p.AnnualizedStateTaxableWages), TextDark);
            layout.Row("Federal Withholding", Money(p.AnnualizedFederalWithholding), Red);
            layout.Row("State Withholding", Money(p.AnnualizedStateWithholding), Red);
            layout.Row("FICA (SS + Medicare)", Money(p.AnnualizedFica), Red);
            layout.Row("Net Pay", Money(p.AnnualizedNetPay), Green, bold: true);

            layout.SectionHeader("Projected Year-to-Date");
            layout.Row("YTD Gross Pay", Money(p.ProjectedYtdGrossPay), TextDark);
            layout.Row("YTD Federal Withholding", Money(p.ProjectedYtdFederalWithholding), Red);
            layout.Row("YTD State Withholding", Money(p.ProjectedYtdStateWithholding), Red);
            layout.Row("YTD FICA", Money(p.ProjectedYtdFica), Red);
            layout.Row("YTD Net Pay", Money(p.ProjectedYtdNetPay), Green);

            layout.SectionHeader("Withholding Estimate");
            layout.Row("Annualized Total Withholding", Money(p.AnnualizedTotalWithholding), Red);
            layout.Row("Est. Federal Tax Liability", Money(p.EstimatedAnnualFederalLiability), TextDark);
            layout.Row("Est. FICA Liability", Money(p.EstimatedAnnualFicaLiability), TextDark);
            layout.Row("Est. Total Tax Liability", Money(p.EstimatedTotalLiability), TextDark);

            layout.Banner(p.OverUnderLabel.ToUpperInvariant(), Money(p.OverUnderAmount), p.IsUnderWithholding ? Red : Green);
        }

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
        public readonly Dictionary<string, int> Images = new();
    }

    private readonly PdfDocument _doc;
    private readonly int _pagesId;
    private readonly int _fontRegular;
    private readonly int _fontBold;
    private readonly List<Page> _pages = new();

    private Page _page = null!;
    private double _y;
    private string _currentTitle = "";
    private int _imageCounter;

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

    public void Image(byte[] jpeg, int pixelWidth, int pixelHeight)
    {
        double displayWidth = ContentWidth;
        double displayHeight = displayWidth * pixelHeight / pixelWidth;
        EnsureSpace(displayHeight + 10);

        int objId = _doc.Add(PdfDocument.Stream(
            $"/Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} " +
            "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
            jpeg));

        string name = $"Im{_imageCounter++}";
        _page.Images[name] = objId;

        double bottom = _y - displayHeight;
        _page.Content.Append("q\n")
            .Append($"{Num(displayWidth)} 0 0 {Num(displayHeight)} {Num(ContentLeft)} {Num(bottom)} cm\n")
            .Append($"/{name} Do\n")
            .Append("Q\n");

        _y -= displayHeight + 12;
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

            var resources = new StringBuilder();
            resources.Append($"/Font << /F1 {_fontRegular} 0 R /F2 {_fontBold} 0 R >>");
            if (page.Images.Count > 0)
            {
                resources.Append(" /XObject << ");
                foreach (var (name, id) in page.Images)
                    resources.Append($"/{name} {id} 0 R ");
                resources.Append(">>");
            }

            int pageId = _doc.Add(
                $"<< /Type /Page /Parent {_pagesId} 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                $"/Resources << {resources} >> /Contents {contentId} 0 R >>");
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
