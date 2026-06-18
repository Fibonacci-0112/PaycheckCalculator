extern alias blazor;
using blazor::PaycheckCalc.Blazor.Models;
using blazor::PaycheckCalc.Blazor.Services.Export;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for the Blazor <see cref="PaycheckCsvRenderer"/> — the per-paycheck CSV
/// export. Expected CSV text is written out explicitly (RFC 4180, CRLF line
/// endings, invariant-culture money as plain decimals) rather than recomputed,
/// so the export format is locked down. The rows mirror the Results panel:
/// Income, Taxes, Deductions, and a Summary.
/// </summary>
public sealed class PaycheckCsvRendererTest
{
    // A fully-populated per-period result with round, explicit values.
    //   TotalTaxes = State 75.00 + SDI 0 + SS 114.70 + Medicare 26.83 + AddlMed 0 + Federal 180.00 = 396.53
    //   NetPay     = Gross 2000.00 - TotalTaxes 396.53 - Deductions 150.00 = 1453.47
    private static PaycheckResult SampleResult() => new()
    {
        GrossPay = 2000.00m,
        FederalTaxableIncome = 1850.00m,
        FicaTaxableWages = 1850.00m,
        StateTaxableWages = 1850.00m,
        FederalWithholding = 180.00m,
        SocialSecurityWithholding = 114.70m,
        MedicareWithholding = 26.83m,
        AdditionalMedicareWithholding = 0m,
        StateWithholding = 75.00m,
        StateDisabilityInsurance = 0m,
        PreTaxDeductions = 150.00m,
        PostTaxDeductions = 0m,
        State = UsState.CA,
        NetPay = 1453.47m
    };

    [Fact]
    public void Render_StandardResult_ProducesExpectedCsv()
    {
        var csv = PaycheckCsvRenderer.Render(SampleResult(), "CA");

        var expected =
            "Section,Item,Value\r\n" +
            "Summary,Tax Year,2026\r\n" +
            "Summary,State,CA\r\n" +
            "Income,Gross Pay,2000.00\r\n" +
            "Income,Federal Taxable Income,1850.00\r\n" +
            "Income,FICA Taxable Income,1850.00\r\n" +
            "Income,State Taxable Income,1850.00\r\n" +
            "Taxes,Federal Tax,180.00\r\n" +
            "Taxes,Social Security Tax,114.70\r\n" +
            "Taxes,Medicare Tax,26.83\r\n" +
            "Taxes,State Income Tax,75.00\r\n" +
            "Deductions,Pre-Tax Deductions,150.00\r\n" +
            "Summary,Total Taxes,396.53\r\n" +
            "Summary,Net Pay,1453.47\r\n";

        Assert.Equal(expected, csv);
    }

    [Fact]
    public void Render_FoldsAdditionalMedicareIntoMedicareLine()
    {
        // Medicare Tax line = MedicareWithholding 290.00 + AdditionalMedicare 18.00 = 308.00
        var result = new PaycheckResult
        {
            GrossPay = 20000.00m,
            FederalTaxableIncome = 20000.00m,
            FicaTaxableWages = 20000.00m,
            StateTaxableWages = 20000.00m,
            FederalWithholding = 4000.00m,
            SocialSecurityWithholding = 0m,
            MedicareWithholding = 290.00m,
            AdditionalMedicareWithholding = 18.00m,
            StateWithholding = 0m,
            State = UsState.TX,
            NetPay = 15692.00m
        };

        var csv = PaycheckCsvRenderer.Render(result, "TX");

        Assert.Contains("Taxes,Medicare Tax,308.00\r\n", csv);
    }

    [Fact]
    public void Render_IncludesDisabilityInsuranceLine_WithItsLabel_WhenPositive()
    {
        var result = new PaycheckResult
        {
            GrossPay = 2000.00m,
            FederalTaxableIncome = 1850.00m,
            FicaTaxableWages = 1850.00m,
            StateTaxableWages = 1850.00m,
            FederalWithholding = 180.00m,
            SocialSecurityWithholding = 114.70m,
            MedicareWithholding = 26.83m,
            StateWithholding = 75.00m,
            StateDisabilityInsurance = 18.50m,
            StateDisabilityInsuranceLabel = "CA SDI",
            State = UsState.CA,
            NetPay = 1434.97m
        };

        var csv = PaycheckCsvRenderer.Render(result, "CA");

        Assert.Contains("Taxes,CA SDI,18.50\r\n", csv);
    }

    [Fact]
    public void Render_QuotesFieldsContainingCommas_PerRfc4180()
    {
        // A disability-insurance label with a comma must be double-quoted.
        var result = new PaycheckResult
        {
            GrossPay = 1000.00m,
            StateDisabilityInsurance = 5.00m,
            StateDisabilityInsuranceLabel = "Paid Family, Medical Leave",
            State = UsState.CT,
            NetPay = 900.00m
        };

        var csv = PaycheckCsvRenderer.Render(result, "CT");

        Assert.Contains("Taxes,\"Paid Family, Medical Leave\",5.00\r\n", csv);
    }

    [Fact]
    public void Render_OmitsStateRow_WhenLabelEmpty()
    {
        var csv = PaycheckCsvRenderer.Render(SampleResult(), "");

        Assert.DoesNotContain("Summary,State,", csv);
        Assert.StartsWith("Section,Item,Value\r\nSummary,Tax Year,2026\r\n", csv);
    }

    [Fact]
    public void Render_OmitsDeductionLines_WhenZero()
    {
        var result = new PaycheckResult
        {
            GrossPay = 1000.00m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            State = UsState.TX,
            NetPay = 923.50m
        };

        var csv = PaycheckCsvRenderer.Render(result, "TX");

        Assert.DoesNotContain("Deductions,", csv);
    }

    [Fact]
    public void Render_IncludesPostTaxDeductionLine_WhenPositive()
    {
        var result = new PaycheckResult
        {
            GrossPay = 1000.00m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 40.00m,
            State = UsState.TX,
            NetPay = 883.50m
        };

        var csv = PaycheckCsvRenderer.Render(result, "TX");

        Assert.Contains("Deductions,Post-Tax Deductions,40.00\r\n", csv);
        Assert.DoesNotContain("Pre-Tax Deductions", csv);
    }

    // ── Annual projection + comparison extensions ────────────────

    //   Annualized at 26 periods, current paycheck #3, 23 remaining.
    private static AnnualProjection SampleProjection() => new()
    {
        PayPeriodsPerYear = 26,
        CurrentPaycheckNumber = 3,
        RemainingPaychecks = 23,
        AnnualizedGrossPay = 52000.00m,
        AnnualizedPreTaxDeductions = 3900.00m,
        AnnualizedFederalWithholding = 4680.00m,
        AnnualizedStateWithholding = 1950.00m,
        AnnualizedFica = 3978.00m,
        AnnualizedNetPay = 37492.00m,
        ProjectedYtdGrossPay = 6000.00m,
        ProjectedYtdFederalWithholding = 540.00m,
        ProjectedYtdStateWithholding = 225.00m,
        ProjectedYtdFica = 459.00m,
        ProjectedYtdNetPay = 4326.00m,
        EstimatedAnnualFederalLiability = 4680.00m,
        EstimatedAnnualFicaLiability = 3978.00m,
        EstimatedTotalLiability = 10608.00m,
        AnnualizedTotalWithholding = 10608.00m,
        OverUnderWithholding = 250.00m
    };

    private static IReadOnlyList<ComparisonRow> SampleComparison() => new[]
    {
        new ComparisonRow("Gross Pay", 2000.00m, 2200.00m),
        new ComparisonRow("Net Pay", 1453.47m, 1600.00m, highlight: true),
    };

    [Fact]
    public void Render_WithoutExtras_OmitsAnnualAndComparisonSections()
    {
        var csv = PaycheckCsvRenderer.Render(SampleResult(), "CA");

        Assert.DoesNotContain("Annualized,", csv);
        Assert.DoesNotContain("Difference", csv);
    }

    [Fact]
    public void Render_WithAnnualProjection_AppendsAnnualRows()
    {
        var csv = PaycheckCsvRenderer.Render(SampleResult(), "CA", SampleProjection());

        Assert.Contains("Annualized,Gross Pay,52000.00\r\n", csv);
        Assert.Contains("Annualized,Pre-Tax Deductions,3900.00\r\n", csv);
        Assert.Contains("Annualized,Net Pay,37492.00\r\n", csv);
        Assert.Contains("Projected YTD,Remaining Paychecks,23\r\n", csv);
        Assert.Contains("Projected YTD,Net Pay,4326.00\r\n", csv);
        Assert.Contains("Year-End Estimate,Estimated Total Liability,10608.00\r\n", csv);
        Assert.Contains("Year-End Estimate,Over/Under Withholding,250.00\r\n", csv);
    }

    [Fact]
    public void Render_WithComparison_AppendsComparisonTable()
    {
        var csv = PaycheckCsvRenderer.Render(SampleResult(), "CA", annual: null,
            comparison: SampleComparison(), comparisonNameA: "Job A", comparisonNameB: "Job B");

        Assert.Contains("Metric,Job A,Job B,Difference\r\n", csv);
        Assert.Contains("Gross Pay,2000.00,2200.00,200.00\r\n", csv);
        Assert.Contains("Net Pay,1453.47,1600.00,146.53\r\n", csv);
    }

    [Fact]
    public void RenderComparison_ProducesFourColumnTable()
    {
        var csv = PaycheckCsvRenderer.RenderComparison(SampleComparison(), "Job A", "Job B");

        Assert.StartsWith("Metric,Job A,Job B,Difference\r\n", csv);
        Assert.Contains("Gross Pay,2000.00,2200.00,200.00\r\n", csv);
    }

    [Fact]
    public void RenderComparison_QuotesNamesContainingCommas()
    {
        var csv = PaycheckCsvRenderer.RenderComparison(SampleComparison(), "Job, A", "Job B");

        Assert.StartsWith("Metric,\"Job, A\",Job B,Difference\r\n", csv);
    }
}
