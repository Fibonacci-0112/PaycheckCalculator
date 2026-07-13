using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Oklahoma;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Task 1 — Tax Year Versioning.
/// Verifies that the tax year flows correctly from inputs through calculators
/// to results and Shared DTOs.
/// </summary>
public sealed class TaxYearVersioningTest
{
    // ── TaxYearSupport constants ────────────────────────────────

    [Fact]
    public void CurrentTaxYear_Is2026()
        => Assert.Equal(2026, TaxYearSupport.CurrentTaxYear);

    [Fact]
    public void SupportedTaxYears_Contains2026()
        => Assert.Contains(2026, TaxYearSupport.SupportedTaxYears);

    // ── Irs15TPercentageCalculator ──────────────────────────────

    [Fact]
    public void FederalCalculator_SupportedTaxYear_Is2026()
    {
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        Assert.Equal(2026, fed.SupportedTaxYear);
    }

    // ── PayCalculator: TaxYear defaults to SupportedTaxYear ────

    [Fact]
    public void Calculate_DefaultTaxYear_ResultHas2026()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.OK
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
            TaxYear = 2025
        });
        Assert.Equal(2025, result.TaxYear);
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
