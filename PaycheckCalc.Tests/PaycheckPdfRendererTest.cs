extern alias blazor;
using System.Text;
using blazor::PaycheckCalc.Blazor.Services.Export;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for the Blazor <see cref="PaycheckPdfRenderer"/> — the single-page
/// per-paycheck PDF export. The generated content stream is uncompressed ASCII,
/// so assertions check both the PDF container structure (header, xref, trailer,
/// catalog/pages/font objects) and that the expected labels and money values
/// appear verbatim in the page content. Money is formatted en-US (e.g.
/// "$2,000.00"), matching the on-screen results.
/// </summary>
public sealed class PaycheckPdfRendererTest
{
    private static PaycheckResult SampleResult() => new()
    {
        GrossPay = 2000.00m,
        FederalTaxableIncome = 1850.00m,
        FicaTaxableWages = 1850.00m,
        StateTaxableWages = 1850.00m,
        FederalWithholding = 180.00m,
        SocialSecurityWithholding = 114.70m,
        MedicareWithholding = 26.83m,
        StateWithholding = 75.00m,
        State = UsState.CA,
        NetPay = 1453.47m
    };

    // Decode raw bytes 1:1 (Latin1) so ASCII content can be searched losslessly.
    private static string AsText(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void Render_ProducesWellFormedPdfContainer()
    {
        var pdf = PaycheckPdfRenderer.Render(SampleResult(), "CA");
        var text = AsText(pdf);

        Assert.True(pdf.Length > 0);
        Assert.StartsWith("%PDF-1.5", text);
        Assert.Contains("xref", text);
        Assert.Contains("trailer", text);
        Assert.Contains("startxref", text);
        Assert.EndsWith("%%EOF", text);
    }

    [Fact]
    public void Render_IncludesCatalogPagesAndFonts()
    {
        var pdf = PaycheckPdfRenderer.Render(SampleResult(), "CA");
        var text = AsText(pdf);

        Assert.Contains("/Type /Catalog", text);
        Assert.Contains("/Type /Pages", text);
        Assert.Contains("/Type /Page", text);
        Assert.Contains("/BaseFont /Helvetica", text);
        Assert.Contains("/BaseFont /Helvetica-Bold", text);
    }

    [Fact]
    public void Render_IncludesTitleWithStateLabel()
    {
        var pdf = PaycheckPdfRenderer.Render(SampleResult(), "CA");
        var text = AsText(pdf);

        Assert.Contains("Paycheck Summary - CA", text);
    }

    [Fact]
    public void Render_WithEmptyStateLabel_OmitsDashInTitle()
    {
        var pdf = PaycheckPdfRenderer.Render(SampleResult(), "");
        var text = AsText(pdf);

        Assert.Contains("Paycheck Summary", text);
        Assert.DoesNotContain("Paycheck Summary -", text);
    }

    [Fact]
    public void Render_IncludesNetPayBannerAndMoneyValues()
    {
        var pdf = PaycheckPdfRenderer.Render(SampleResult(), "CA");
        var text = AsText(pdf);

        Assert.Contains("NET PAY", text);
        Assert.Contains("$2,000.00", text);   // gross
        Assert.Contains("$1,453.47", text);   // net
        Assert.Contains("Federal Tax", text);
        Assert.Contains("Social Security Tax", text);
    }

    [Fact]
    public void Render_IncludesDisabilityInsuranceRow_WhenPositive()
    {
        var result = new PaycheckResult
        {
            GrossPay = 2000.00m,
            FederalWithholding = 180.00m,
            SocialSecurityWithholding = 114.70m,
            MedicareWithholding = 26.83m,
            StateWithholding = 75.00m,
            StateDisabilityInsurance = 18.50m,
            StateDisabilityInsuranceLabel = "CA SDI",
            State = UsState.CA,
            NetPay = 1434.97m
        };

        var text = AsText(PaycheckPdfRenderer.Render(result, "CA"));

        Assert.Contains("CA SDI", text);
        Assert.Contains("$18.50", text);
    }
}
