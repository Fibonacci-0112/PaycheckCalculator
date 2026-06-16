using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Supplemental;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="StateSupplementalCalculator"/>, driven by the production
/// <c>state_supplemental_2026.json</c> table. Expected values are the explicit per-state
/// rate arithmetic from that table, not recomputed with the calculator.
/// </summary>
public sealed class StateSupplementalCalculatorTest
{
    private static StateSupplementalCalculator Create()
        => new(File.ReadAllText("state_supplemental_2026.json"));

    [Theory]
    // Flat-rate states: bonus × published supplemental rate.
    [InlineData(UsState.CA, 5_000, 511.50)]   // 10.23%
    [InlineData(UsState.OK, 5_000, 225.00)]   // 4.5%
    [InlineData(UsState.NY, 10_000, 1_170.00)] // 11.7%
    [InlineData(UsState.ND, 5_000, 75.00)]    // 1.5%
    [InlineData(UsState.MN, 4_000, 250.00)]   // 6.25%
    [InlineData(UsState.OR, 1_000, 80.00)]    // 8.0%
    public void FlatRateStates_ApplyPublishedRate(UsState state, decimal bonus, decimal expected)
    {
        var calc = Create();
        var result = calc.Calculate(state, bonus, federalSupplementalWithholding: 0m);

        Assert.Equal(StateSupplementalMethod.FlatRate, result.Method);
        Assert.Equal(expected, result.Withholding);
        Assert.False(result.UsesRegularMethod);
    }

    [Theory]
    [InlineData(UsState.TX)]
    [InlineData(UsState.FL)]
    [InlineData(UsState.WA)]
    [InlineData(UsState.NH)]
    public void NoIncomeTaxStates_WithholdZero(UsState state)
    {
        var calc = Create();
        var result = calc.Calculate(state, 5_000m, federalSupplementalWithholding: 0m);

        Assert.Equal(StateSupplementalMethod.NoIncomeTax, result.Method);
        Assert.Equal(0m, result.Withholding);
        Assert.False(result.UsesRegularMethod);
    }

    [Fact]
    public void Vermont_Withholds30PercentOfFederal()
    {
        // VT = 30% of the federal supplemental withholding ($1,100 on a $5,000 bonus).
        //   1,100 × 30% = 330.00
        var calc = Create();
        var result = calc.Calculate(UsState.VT, 5_000m, federalSupplementalWithholding: 1_100m);

        Assert.Equal(StateSupplementalMethod.FederalPercentage, result.Method);
        Assert.Equal(330.00m, result.Withholding);
        Assert.False(result.UsesRegularMethod);
    }

    [Theory]
    [InlineData(UsState.PA)]
    [InlineData(UsState.IL)]
    [InlineData(UsState.GA)]
    [InlineData(UsState.MA)]
    public void RegularMethodStates_WithholdZeroAndFlag(UsState state)
    {
        var calc = Create();
        var result = calc.Calculate(state, 5_000m, federalSupplementalWithholding: 1_100m);

        Assert.Equal(StateSupplementalMethod.RegularMethod, result.Method);
        Assert.Equal(0m, result.Withholding);
        Assert.True(result.UsesRegularMethod);
        Assert.False(string.IsNullOrWhiteSpace(result.Description));
    }

    [Fact]
    public void AllStatesAndDc_HaveAnEntry()
    {
        var calc = Create();
        foreach (var state in Enum.GetValues<UsState>())
        {
            var rate = calc.GetRate(state);
            // FlatRate / FederalPercentage entries must carry a positive rate.
            if (rate.Method is StateSupplementalMethod.FlatRate or StateSupplementalMethod.FederalPercentage)
                Assert.True(rate.Rate > 0m, $"{state} should have a positive rate");
        }
    }

    [Fact]
    public void TaxYear_Is2026()
    {
        Assert.Equal(2026, Create().TaxYear);
    }
}
