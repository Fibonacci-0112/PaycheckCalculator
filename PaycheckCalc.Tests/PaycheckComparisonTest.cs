extern alias blazor;
using blazor::PaycheckCalc.Blazor.Models;
using blazor::PaycheckCalc.Blazor.Services;
using PaycheckCalc.Shared.Snapshots;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for the Blazor <see cref="PaycheckComparison"/> builder — the A/B
/// side-by-side comparison of two saved paychecks. Expected values and the
/// B − A differences are written out explicitly. Conditional rows (state
/// disability, pre/post-tax deductions) appear only when either side is
/// non-zero, Medicare folds in Additional Medicare, and the Net Pay row is
/// flagged for emphasis — mirroring the MAUI Paychecks page.
/// </summary>
public sealed class PaycheckComparisonTest
{
    // Two consistent sample results. The builder reads the stored numbers
    // verbatim, so only the fields each row references matter.
    private static SavedPaycheckResultDto ResultA() => new()
    {
        GrossPay = 2000.00m,
        FederalWithholding = 180.00m,
        SocialSecurityWithholding = 124.00m,
        MedicareWithholding = 29.00m,
        AdditionalMedicareWithholding = 0m,
        StateWithholding = 75.00m,
        StateDisabilityInsurance = 0m,
        PreTaxDeductions = 100.00m,
        PostTaxDeductions = 0m,
        TotalTaxes = 408.00m,
        NetPay = 1492.00m
    };

    private static SavedPaycheckResultDto ResultB() => new()
    {
        GrossPay = 3000.00m,
        FederalWithholding = 300.00m,
        SocialSecurityWithholding = 186.00m,
        MedicareWithholding = 43.50m,
        AdditionalMedicareWithholding = 0m,
        StateWithholding = 120.00m,
        StateDisabilityInsurance = 0m,
        PreTaxDeductions = 150.00m,
        PostTaxDeductions = 0m,
        TotalTaxes = 649.50m,
        NetPay = 2200.50m
    };

    private static ComparisonRow Row(IReadOnlyList<ComparisonRow> rows, string label) =>
        rows.Single(r => r.Label == label);

    [Fact]
    public void BuildRows_ProducesExpectedRowsInOrder()
    {
        var rows = PaycheckComparison.BuildRows(ResultA(), ResultB());

        // Both SDI and post-tax are zero on both sides, so those rows are omitted;
        // pre-tax is non-zero, so it is present.
        Assert.Equal(
            new[]
            {
                "Gross Pay", "Federal Tax", "Social Security", "Medicare",
                "State Income Tax", "Pre-Tax Deductions", "Total Taxes", "Net Pay"
            },
            rows.Select(r => r.Label).ToArray());
    }

    [Fact]
    public void BuildRows_CopiesValuesAndComputesBMinusADifference()
    {
        var rows = PaycheckComparison.BuildRows(ResultA(), ResultB());

        var gross = Row(rows, "Gross Pay");
        Assert.Equal(2000.00m, gross.ValueA);
        Assert.Equal(3000.00m, gross.ValueB);
        Assert.Equal(1000.00m, gross.Difference);

        var federal = Row(rows, "Federal Tax");
        Assert.Equal(120.00m, federal.Difference);

        var net = Row(rows, "Net Pay");
        Assert.Equal(1492.00m, net.ValueA);
        Assert.Equal(2200.50m, net.ValueB);
        Assert.Equal(708.50m, net.Difference);
    }

    [Fact]
    public void BuildRows_FoldsAdditionalMedicareIntoMedicareRow()
    {
        var a = ResultA() with { MedicareWithholding = 200.00m, AdditionalMedicareWithholding = 18.00m };
        var b = ResultB() with { MedicareWithholding = 250.00m, AdditionalMedicareWithholding = 0m };

        var medicare = Row(PaycheckComparison.BuildRows(a, b), "Medicare");

        Assert.Equal(218.00m, medicare.ValueA);   // 200.00 + 18.00
        Assert.Equal(250.00m, medicare.ValueB);
        Assert.Equal(32.00m, medicare.Difference);
    }

    [Fact]
    public void BuildRows_IncludesStateDisability_WhenEitherSidePositive()
    {
        var a = ResultA();                                          // SDI 0
        var b = ResultB() with { StateDisabilityInsurance = 18.50m };

        var rows = PaycheckComparison.BuildRows(a, b);

        var sdi = Row(rows, "State Disability");
        Assert.Equal(0m, sdi.ValueA);
        Assert.Equal(18.50m, sdi.ValueB);
    }

    [Fact]
    public void BuildRows_IncludesPostTaxDeductions_WhenEitherSidePositive()
    {
        var a = ResultA() with { PostTaxDeductions = 40.00m };
        var b = ResultB();                                          // post-tax 0

        Assert.Contains(PaycheckComparison.BuildRows(a, b), r => r.Label == "Post-Tax Deductions");
    }

    [Fact]
    public void BuildRows_OmitsConditionalRows_WhenBothSidesZero()
    {
        // No SDI, no pre-tax, no post-tax on either side.
        var a = ResultA() with { PreTaxDeductions = 0m };
        var b = ResultB() with { PreTaxDeductions = 0m };

        var labels = PaycheckComparison.BuildRows(a, b).Select(r => r.Label).ToArray();

        Assert.DoesNotContain("State Disability", labels);
        Assert.DoesNotContain("Pre-Tax Deductions", labels);
        Assert.DoesNotContain("Post-Tax Deductions", labels);
        Assert.Equal(
            new[] { "Gross Pay", "Federal Tax", "Social Security", "Medicare", "State Income Tax", "Total Taxes", "Net Pay" },
            labels);
    }

    [Fact]
    public void BuildRows_HighlightsOnlyNetPayRow()
    {
        var rows = PaycheckComparison.BuildRows(ResultA(), ResultB());

        Assert.True(Row(rows, "Net Pay").Highlight);
        Assert.All(rows.Where(r => r.Label != "Net Pay"), r => Assert.False(r.Highlight));
    }

    [Fact]
    public void DifferenceDisplay_PositiveDifference_IsPlusSignedUsCurrency()
    {
        var gross = Row(PaycheckComparison.BuildRows(ResultA(), ResultB()), "Gross Pay");

        Assert.Equal("+$1,000.00", gross.DifferenceDisplay);
    }

    [Fact]
    public void DifferenceDisplay_NegativeDifference_UsesUnicodeMinus()
    {
        // Swap A/B so B nets less than A → negative difference.
        var net = Row(PaycheckComparison.BuildRows(ResultB(), ResultA()), "Net Pay");

        Assert.Equal(-708.50m, net.Difference);
        Assert.Equal("−$708.50", net.DifferenceDisplay);   // U+2212 MINUS SIGN, not hyphen
    }

    [Fact]
    public void ValueDisplays_AreUsCurrency()
    {
        var gross = Row(PaycheckComparison.BuildRows(ResultA(), ResultB()), "Gross Pay");

        Assert.Equal("$2,000.00", gross.ValueADisplay);
        Assert.Equal("$3,000.00", gross.ValueBDisplay);
    }
}
