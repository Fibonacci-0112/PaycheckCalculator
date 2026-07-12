using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Federal;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for the step-by-step explanation produced alongside federal withholding
/// in <see cref="Irs15TPercentageCalculator.CalculateWithExplanation"/>.
/// Asserts on intermediate worksheet values that the production code now exposes.
/// </summary>
public sealed class Irs15TExplanationTest
{
    private static Irs15TPercentageCalculator CreateCalculator()
    {
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        return new Irs15TPercentageCalculator(fedJson);
    }

    [Fact]
    public void Explanation_HasFederalLineKey_AndPub15TReference()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        Assert.Equal(ExplanationLineKey.FederalWithholding, explanation.Key);
        Assert.Equal("Federal Withholding", explanation.Title);
        Assert.NotNull(explanation.Reference);
        Assert.Contains("Pub", explanation.Reference!);
        Assert.Contains("15-T", explanation.Reference!);
    }

    [Fact]
    public void ZeroTaxableWages_EmitsNoWithholdingStep_AndFinalAmountIsZero()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (withholding, explanation) = calc.CalculateWithExplanation(0m, PayFrequency.Biweekly, w4);

        Assert.Equal(0m, withholding);
        Assert.Equal(0m, explanation.FinalAmount);
        Assert.Contains(explanation.Steps, s => s.Label == "No withholding");
    }

    [Fact]
    public void SingleBiweekly_AnnualizationStep_MultipliesBy26()
    {
        // $2,000 biweekly × 26 = $52,000 annualized.
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        var annualizeStep = Assert.Single(explanation.Steps, s => s.Label.StartsWith("Step 1a"));
        Assert.Equal(52_000m, annualizeStep.Value);
        Assert.Contains("26", annualizeStep.Label);
    }

    [Fact]
    public void Single_StandardDeduction_ReportsOtherFilingAmount()
    {
        // Single filer, Step 2 not checked, so the worksheet inserts the Single
        // built-in standard deduction ($15,000 in the 2026 Pub 15-T tables).
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        var step1g = Assert.Single(explanation.Steps, s => s.Label.StartsWith("Step 1g"));
        Assert.NotNull(step1g.Value);
        Assert.True(step1g.Value!.Value > 0m, "Standard deduction should be positive for a non-Step-2-checked Single filer.");
    }

    [Fact]
    public void Step2Checked_StandardDeductionIsZero()
    {
        // When W-4 Step 2 is checked, the worksheet zeroes out the built-in
        // standard deduction because the dual-job tables already account for it.
        var calc = CreateCalculator();
        var w4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step2Checked = true
        };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        var step1g = Assert.Single(explanation.Steps, s => s.Label.StartsWith("Step 1g"));
        Assert.Equal(0m, step1g.Value);
    }

    [Fact]
    public void StepsAppearInWorksheetOrder()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        var labels = explanation.Steps.Select(s => s.Label).ToList();
        var taxableIndex = labels.FindIndex(l => l.Contains("Federal taxable wages"));
        var annualizeIndex = labels.FindIndex(l => l.StartsWith("Step 1a"));
        var bracketIndex = labels.FindIndex(l => l.Contains("Locate the annual tax bracket"));
        var deannualizeIndex = labels.FindIndex(l => l.Contains("De-annualize"));

        Assert.True(taxableIndex < annualizeIndex, "Taxable wages must appear before annualization.");
        Assert.True(annualizeIndex < bracketIndex, "Annualization must appear before bracket lookup.");
        Assert.True(bracketIndex < deannualizeIndex, "Bracket lookup must appear before de-annualization.");
    }

    [Fact]
    public void ExtraWithholding_StepIsEmitted_AndAddsToFinal()
    {
        // $25 extra per pay period on W-4 Step 4(c) should appear as its own
        // step and be added to the final withholding.
        var calc = CreateCalculator();
        var w4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step4cExtraWithholding = 25m
        };

        var (withholdingWithExtra, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);
        var (withholdingWithoutExtra, _) = calc.CalculateWithExplanation(
            2000m,
            PayFrequency.Biweekly,
            new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately });

        Assert.Equal(withholdingWithoutExtra + 25m, withholdingWithExtra);
        Assert.Contains(explanation.Steps, s => s.Label.StartsWith("Step 4(c)"));
    }

    [Fact]
    public void NoExtraWithholding_StepIsOmitted()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var (_, explanation) = calc.CalculateWithExplanation(2000m, PayFrequency.Biweekly, w4);

        Assert.DoesNotContain(explanation.Steps, s => s.Label.StartsWith("Step 4(c)"));
    }

    [Fact]
    public void FinalRoundedStep_MatchesWithholdingAmount()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.MarriedFilingJointly };

        var (withholding, explanation) = calc.CalculateWithExplanation(3000m, PayFrequency.Biweekly, w4);

        var finalStep = explanation.Steps.Last();
        Assert.Equal("Federal withholding (rounded)", finalStep.Label);
        Assert.Equal(withholding, finalStep.Value);
        Assert.Equal(withholding, explanation.FinalAmount);
    }

    [Fact]
    public void CalculateWithholding_LegacyApi_ReturnsSameAmountAsExplanationApi()
    {
        var calc = CreateCalculator();
        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };

        var legacy = calc.CalculateWithholding(2500m, PayFrequency.Biweekly, w4);
        var (detailed, _) = calc.CalculateWithExplanation(2500m, PayFrequency.Biweekly, w4);

        Assert.Equal(legacy, detailed);
    }
}
