extern alias blazor;
using System.Text;
using blazor::PaycheckCalculator.Blazor.Models;
using blazor::PaycheckCalculator.Blazor.Services.Export;
using PaycheckCalculator.Core.Models;
using Xunit;

namespace PaycheckCalculator.Tests;

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

    // ── Annual projection + comparison extensions ────────────────

    private static AnnualProjection SampleProjection(decimal overUnder = 250.00m) => new()
    {
        PayPeriodsPerYear = 26,
        CurrentPaycheckNumber = 3,
        RemainingPaychecks = 23,
        AnnualizedGrossPay = 52000.00m,
        AnnualizedFederalWithholding = 4680.00m,
        AnnualizedStateWithholding = 1950.00m,
        AnnualizedFica = 3978.00m,
        AnnualizedNetPay = 37492.00m,
        ProjectedYtdGrossPay = 6000.00m,
        ProjectedYtdNetPay = 4326.00m,
        EstimatedTotalLiability = 10608.00m,
        AnnualizedTotalWithholding = 10858.00m,
        OverUnderWithholding = overUnder
    };

    private static IReadOnlyList<ComparisonRow> SampleComparison() => new[]
    {
        new ComparisonRow("Gross Pay", 2000.00m, 2200.00m),
        new ComparisonRow("Net Pay", 1453.47m, 1600.00m, highlight: true),
    };

    [Fact]
    public void Render_WithoutExtras_OmitsAnnualAndComparison()
    {
        var text = AsText(PaycheckPdfRenderer.Render(SampleResult(), "CA"));

        Assert.DoesNotContain("ANNUALIZED AMOUNTS", text);
        Assert.DoesNotContain("PAYCHECK COMPARISON", text);
    }

    [Fact]
    public void Render_WithAnnualProjection_IncludesAnnualSectionsAndRefund()
    {
        var text = AsText(PaycheckPdfRenderer.Render(SampleResult(), "CA", SampleProjection(overUnder: 250.00m)));

        // Parentheses in PDF text are escaped (\( \)), so assert the unparenthesized prefix.
        Assert.Contains("ANNUALIZED AMOUNTS", text);
        Assert.Contains("PROJECTED YEAR-TO-DATE - PAYCHECK 3 OF 26", text);
        Assert.Contains("YEAR-END ESTIMATE", text);
        Assert.Contains("Estimated Refund", text);
        Assert.Contains("$52,000.00", text);
    }

    [Fact]
    public void Render_WithUnderWithholding_ShowsAmountOwed()
    {
        var text = AsText(PaycheckPdfRenderer.Render(SampleResult(), "CA", SampleProjection(overUnder: -125.00m)));

        Assert.Contains("Estimated Amount Owed", text);
        Assert.DoesNotContain("Estimated Refund", text);
    }

    [Fact]
    public void Render_WithComparison_IncludesComparisonSection()
    {
        var text = AsText(PaycheckPdfRenderer.Render(SampleResult(), "CA", annual: null,
            comparison: SampleComparison(), comparisonNameA: "Job A", comparisonNameB: "Job B"));

        Assert.Contains("PAYCHECK COMPARISON", text);
        Assert.Contains("Metric", text);
        Assert.Contains("Diff", text);
    }

    [Fact]
    public void RenderComparison_ProducesStandaloneComparisonSheet()
    {
        var text = AsText(PaycheckPdfRenderer.RenderComparison(SampleComparison(), "Job A", "Job B"));

        Assert.StartsWith("%PDF-1.5", text);
        Assert.Contains("Paycheck Comparison", text);
        Assert.Contains("PAYCHECK COMPARISON", text);
        Assert.Contains("Gross Pay", text);
    }
}
