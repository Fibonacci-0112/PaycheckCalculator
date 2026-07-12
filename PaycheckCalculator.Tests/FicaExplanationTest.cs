using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Tax.Fica;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for the step-by-step explanation produced by
/// <see cref="FicaCalculator.CalculateWithExplanation"/>.
/// </summary>
public sealed class FicaExplanationTest
{
    [Fact]
    public void SocialSecurity_NoYtd_AllWagesTaxable()
    {
        // $1,000 wages this period, $0 YTD: full $1,000 subject to SS at 6.2%.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(1000m, ytdSsWages: 0m, ytdMedicareWages: 0m);

        Assert.Equal(62m, result.SocialSecurity);
        Assert.Equal(ExplanationLineKey.SocialSecurity, result.SocialSecurityExplanation.Key);
        var ssTaxable = Assert.Single(result.SocialSecurityExplanation.Steps,
            s => s.Label == "Wages subject to Social Security this period");
        Assert.Equal(1000m, ssTaxable.Value);
    }

    [Fact]
    public void SocialSecurity_AboveWageBase_TaxableCappedAtRemaining()
    {
        // YTD already at $184,000, period wages $5,000. SS base = $184,500.
        // Remaining base = $500; taxable = min(5000, 500) = $500; SS = 500 × 6.2% = $31.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(5000m, ytdSsWages: 184_000m, ytdMedicareWages: 184_000m);

        Assert.Equal(31m, result.SocialSecurity);
        var remainingStep = Assert.Single(result.SocialSecurityExplanation.Steps,
            s => s.Label == "Remaining Social Security wage base");
        Assert.Equal(500m, remainingStep.Value);
        var taxableStep = Assert.Single(result.SocialSecurityExplanation.Steps,
            s => s.Label == "Wages subject to Social Security this period");
        Assert.Equal(500m, taxableStep.Value);
    }

    [Fact]
    public void SocialSecurity_FullyAtWageBase_TaxableIsZero()
    {
        // YTD already at the wage base: remaining = 0, SS = 0.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(5000m, ytdSsWages: 184_500m, ytdMedicareWages: 184_500m);

        Assert.Equal(0m, result.SocialSecurity);
        var remainingStep = Assert.Single(result.SocialSecurityExplanation.Steps,
            s => s.Label == "Remaining Social Security wage base");
        Assert.Equal(0m, remainingStep.Value);
    }

    [Fact]
    public void Medicare_FlatRateOnAllWages_NoWageBaseStep()
    {
        // Medicare has no wage base.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(2000m, ytdSsWages: 0m, ytdMedicareWages: 0m);

        Assert.Equal(29m, result.Medicare); // 2000 × 1.45%
        Assert.Equal(ExplanationLineKey.Medicare, result.MedicareExplanation.Key);
        // Medicare explanation should not mention a wage base.
        Assert.DoesNotContain(result.MedicareExplanation.Steps,
            s => s.Label.Contains("wage base", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdditionalMedicare_BelowThreshold_IsZero()
    {
        // Crossing well below $200,000 employer threshold: no Additional Medicare.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(5000m, ytdSsWages: 50_000m, ytdMedicareWages: 50_000m);

        Assert.Equal(0m, result.AdditionalMedicare);
        var overStep = Assert.Single(result.AdditionalMedicareExplanation.Steps,
            s => s.Label.Contains("above the"));
        Assert.Equal(0m, overStep.Value);
    }

    [Fact]
    public void AdditionalMedicare_CrossingThreshold_OnlyPortionAboveIsTaxed()
    {
        // YTD $195,000, period $10,000. Crossing $200,000 → only $5,000 is "over".
        // Additional Medicare = $5,000 × 0.9% = $45.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(10_000m, ytdSsWages: 195_000m, ytdMedicareWages: 195_000m);

        Assert.Equal(45m, result.AdditionalMedicare);
        var overStep = Assert.Single(result.AdditionalMedicareExplanation.Steps,
            s => s.Label.Contains("above the"));
        Assert.Equal(5_000m, overStep.Value);
    }

    [Fact]
    public void AdditionalMedicare_FullyAboveThreshold_AllWagesTaxed()
    {
        // YTD $210,000 — already above $200,000 — so the full $10,000 this period is "over".
        // 0.9% × $10,000 = $90.
        var calc = new FicaCalculator();

        var result = calc.CalculateWithExplanation(10_000m, ytdSsWages: 184_500m, ytdMedicareWages: 210_000m);

        Assert.Equal(90m, result.AdditionalMedicare);
        var overStep = Assert.Single(result.AdditionalMedicareExplanation.Steps,
            s => s.Label.Contains("above the"));
        Assert.Equal(10_000m, overStep.Value);
    }

    [Fact]
    public void LegacyTupleApi_ReturnsSameValuesAsExplanationApi()
    {
        var calc = new FicaCalculator();

        var (ss, medicare, addl) = calc.Calculate(1500m, 0m, 0m);
        var detailed = calc.CalculateWithExplanation(1500m, 0m, 0m);

        Assert.Equal(ss, detailed.SocialSecurity);
        Assert.Equal(medicare, detailed.Medicare);
        Assert.Equal(addl, detailed.AdditionalMedicare);
    }
}
