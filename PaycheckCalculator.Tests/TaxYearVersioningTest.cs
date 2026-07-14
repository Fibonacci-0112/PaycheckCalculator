using System.Text.Json;
using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Oklahoma;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Core.Tax.Supplemental;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for tax-year versioning: <see cref="TaxYearSupport"/> and the <c>TaxYear</c> field now
/// carried on every input/result model. Only 2026 tax data is loaded today, so
/// <see cref="TaxYearSupport.Default"/> is both the implicit default for new inputs and the only
/// currently-supported year; calculators must reject any other year rather than silently applying
/// 2026 tables to it.
/// </summary>
public sealed class TaxYearVersioningTest
{
    [Fact]
    public void TaxYearSupport_Default_Is2026()
    {
        Assert.Equal(2026, TaxYearSupport.Default);
    }

    [Theory]
    [InlineData(2026, true)]
    [InlineData(2025, false)]
    [InlineData(2027, false)]
    public void TaxYearSupport_IsSupported_OnlyMatchesDefault(int year, bool expected)
    {
        Assert.Equal(expected, TaxYearSupport.IsSupported(year));
    }

    [Fact]
    public void PaycheckInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new PaycheckInput().TaxYear);
    }

    [Fact]
    public void SelfEmploymentInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new SelfEmploymentInput().TaxYear);
    }

    [Fact]
    public void BonusInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new BonusInput().TaxYear);
    }

    // ── PayCalculator ──────────────────────────────────────────────

    [Fact]
    public void PayCalculator_Result_CarriesInputTaxYear()
    {
        var calculator = CreateCalculator();
        var result = calculator.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.OK,
            TaxYear = 2026
        });

        Assert.Equal(2026, result.TaxYear);
    }

    [Fact]
    public void Calculate_ExplicitTaxYear_ResultPreservesIt()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.OK,
            TaxYear = 2026
        });
        Assert.Equal(2026, result.TaxYear);
    }

    // ── BonusCalculator ─────────────────────────────────────────

    [Fact]
    public void BonusCalculate_DefaultTaxYear_ResultHas2026()
    {
        var calc = CreateBonusCalculator();
        var result = calc.Calculate(new BonusInput
        {
            BonusAmount = 5000m,
            State = UsState.OK
        });
        Assert.Equal(2026, result.TaxYear);
    }

    // ── SelfEmploymentCalculator ────────────────────────────────

    [Fact]
    public void SelfEmploymentCalculate_DefaultTaxYear_ResultHas2026()
    {
        var calc = CreateSelfEmploymentCalculator();
        var result = calc.Calculate(new SelfEmploymentInput
        {
            AnnualNetEarnings = 80_000m,
            State = UsState.OK
        });
        Assert.Equal(2026, result.TaxYear);
    }

    // ── Shared SavedPaycheckResultMapper ────────────────────────

    [Fact]
    public void SavedPaycheckResultMapper_PreservesTaxYear()
    {
        var result = new PaycheckResult { TaxYear = 2026, GrossPay = 2000m, NetPay = 1600m };
        var dto = PaycheckCalculator.Shared.Snapshots.SavedPaycheckResultMapper.FromResult(result);
        Assert.Equal(2026, dto.TaxYear);
    }

    // ── SourceCitation / PaycheckExplanation.Sources ────────────

    [Fact]
    public void PaycheckExplanation_Sources_AggregatesFromLines()
    {
        var lines = new[]
        {
            new LineExplanation(ExplanationLineKey.FederalWithholding, "Federal Withholding", 100m,
                Array.Empty<ExplanationStep>(),
                "IRS Publication 15-T (2026), Worksheet 1A"),
            new LineExplanation(ExplanationLineKey.SocialSecurity, "Social Security", 62m,
                Array.Empty<ExplanationStep>(),
                "IRS Publication 15 (2026), Section 5"),
            new LineExplanation(ExplanationLineKey.GrossPay, "Gross Pay", 1000m,
                Array.Empty<ExplanationStep>())
        };
        var explanation = new PaycheckExplanation(lines);

        Assert.Equal(2, explanation.Sources.Count);
        Assert.Contains(explanation.Sources, s => s.Label == "Federal Withholding");
        Assert.Contains(explanation.Sources, s => s.Reference == "IRS Publication 15 (2026), Section 5");
    }

    [Fact]
    public void PaycheckExplanation_Sources_DeduplicatesBySameReference()
    {
        const string sharedRef = "IRS Publication 15-T (2026), Worksheet 1A";
        var lines = new[]
        {
            new LineExplanation(ExplanationLineKey.FederalWithholding, "Federal Withholding", 100m,
                Array.Empty<ExplanationStep>(), sharedRef),
            new LineExplanation(ExplanationLineKey.GrossPay, "Gross Pay", 2000m,
                Array.Empty<ExplanationStep>(), sharedRef)
        };
        var explanation = new PaycheckExplanation(lines);

        // Both lines share the same reference — deduplicated to one citation.
        Assert.Single(explanation.Sources);
        Assert.Equal("Federal Withholding", explanation.Sources[0].Label);
    }

    [Fact]
    public void PaycheckExplanation_Empty_HasNoSources()
        => Assert.Empty(PaycheckExplanation.Empty.Sources);

    [Fact]
    public void RealPaycheckResult_Sources_ContainsFederalReference()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.OK
        });
        // Federal withholding line should carry an IRS 15-T reference.
        Assert.NotEmpty(result.Explanation.Sources);
        Assert.Contains(result.Explanation.Sources, s => s.Reference.Contains("15-T"));
    }

    // ── Helpers ────────────────────────────────────────────────

    private static PayCalculator CreateCalculator()
    {
        var registry = new StateCalculatorRegistry();
        var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }

    private static BonusCalculator CreateBonusCalculator()
    {
        var suppJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "state_supplemental_2026.json"));
        var state = new PaycheckCalculator.Core.Tax.Supplemental.StateSupplementalCalculator(suppJson);
        return new BonusCalculator(new PaycheckCalculator.Core.Tax.Federal.FederalSupplementalCalculator(), new FicaCalculator(), state);
    }

    private static SelfEmploymentCalculator CreateSelfEmploymentCalculator()
    {
        var registry = new StateCalculatorRegistry();
        var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
        return new SelfEmploymentCalculator(registry, new FicaCalculator());
    }
}
