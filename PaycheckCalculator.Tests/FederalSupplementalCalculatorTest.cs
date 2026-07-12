using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Tax.Federal;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="FederalSupplementalCalculator"/> — the IRS Pub 15 §7 flat-rate
/// method for supplemental wages: 22% up to the cumulative $1,000,000 annual threshold,
/// 37% above it. Expected values are the explicit flat-rate arithmetic, not recomputed
/// with the calculator.
/// </summary>
public sealed class FederalSupplementalCalculatorTest
{
    [Fact]
    public void FlatRate_BonusUnderThreshold_Withholds22Percent()
    {
        // $5,000 × 22% = $1,100.00
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(1_100.00m, calc.Calculate(5_000m));
    }

    [Theory]
    [InlineData(1_000, 220.00)]
    [InlineData(5_000, 1_100.00)]
    [InlineData(25_000, 5_500.00)]
    [InlineData(0, 0.00)]
    public void FlatRate_VariousBonuses_Withholds22Percent(decimal bonus, decimal expected)
    {
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(expected, calc.Calculate(bonus));
    }

    [Fact]
    public void Bonus_StraddlingThreshold_SplitsBetween22And37()
    {
        // $2,000,000 with no prior supplemental wages:
        //   first $1,000,000 × 22% = 220,000
        //   next  $1,000,000 × 37% = 370,000  → total 590,000.00
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(590_000.00m, calc.Calculate(2_000_000m));
    }

    [Fact]
    public void Bonus_PartlyOverThreshold_UsesYtdSupplementalWages()
    {
        // $5,000 bonus, $998,000 already paid this year:
        //   remaining under $1M = 2,000 → 2,000 × 22% = 440
        //   over $1M            = 3,000 → 3,000 × 37% = 1,110  → total 1,550.00
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(1_550.00m, calc.Calculate(5_000m, ytdSupplementalWages: 998_000m));
    }

    [Fact]
    public void Bonus_FullyOverThreshold_AllAt37()
    {
        // Already past $1M for the year → entire payment at 37%.
        //   $500,000 × 37% = 185,000.00
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(185_000.00m, calc.Calculate(500_000m, ytdSupplementalWages: 1_200_000m));
    }

    [Fact]
    public void NegativeBonus_TreatedAsZero()
    {
        var calc = new FederalSupplementalCalculator();
        Assert.Equal(0m, calc.Calculate(-100m));
    }

    [Fact]
    public void Explanation_ReportsRateBreakdown()
    {
        var calc = new FederalSupplementalCalculator();
        var (withholding, explanation) = calc.CalculateWithExplanation(5_000m);

        Assert.Equal(1_100.00m, withholding);
        Assert.Equal(ExplanationLineKey.FederalWithholding, explanation.Key);
        Assert.Equal(withholding, explanation.FinalAmount);
        Assert.NotEmpty(explanation.Steps);
    }
}
