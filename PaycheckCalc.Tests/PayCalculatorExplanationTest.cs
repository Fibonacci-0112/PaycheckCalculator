using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Verifies that <see cref="PayCalculator"/> assembles a complete
/// <see cref="PaycheckExplanation"/> alongside each <see cref="PaycheckResult"/>.
/// </summary>
public sealed class PayCalculatorExplanationTest
{
    [Fact]
    public void Result_CarriesNonEmptyExplanation()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.NotNull(result.Explanation);
        Assert.NotEmpty(result.Explanation.Lines);
    }

    [Fact]
    public void Explanation_IncludesGrossFederalSocialSecurityMedicareStateAndNet()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.GrossPay));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.FederalWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.SocialSecurity));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.Medicare));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.StateWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.NetPay));
    }

    [Fact]
    public void AdditionalMedicare_ExplanationOmitted_WhenZero()
    {
        // Normal wages ($1,440 biweekly) never cross the $200k threshold, so the
        // Additional Medicare line is zero and its explanation should be omitted.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.Equal(0m, result.AdditionalMedicareWithholding);
        Assert.Null(result.Explanation.Get(ExplanationLineKey.AdditionalMedicare));
    }

    [Fact]
    public void FederalExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var federalExpl = result.Explanation.Get(ExplanationLineKey.FederalWithholding);
        Assert.NotNull(federalExpl);
        Assert.Equal(result.FederalWithholding, federalExpl!.FinalAmount);
    }

    [Fact]
    public void SocialSecurityExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var ssExpl = result.Explanation.Get(ExplanationLineKey.SocialSecurity);
        Assert.NotNull(ssExpl);
        Assert.Equal(result.SocialSecurityWithholding, ssExpl!.FinalAmount);
    }

    [Fact]
    public void StateExplanation_ContainsTaxableWagesStep()
    {
        // Generic fallback: even though Oklahoma's calculator doesn't yet opt in
        // to a custom explanation, PayCalculator builds a wage-base + withholding
        // breakdown from the StateWithholdingResult.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var stateExpl = result.Explanation.Get(ExplanationLineKey.StateWithholding);
        Assert.NotNull(stateExpl);
        Assert.Contains(stateExpl!.Steps, s => s.Label == "State taxable wages");
    }

    [Fact]
    public void NetExplanation_AggregatesGrossLessDeductionsLessTaxes()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var netExpl = result.Explanation.Get(ExplanationLineKey.NetPay);
        Assert.NotNull(netExpl);
        var netStep = Assert.Single(netExpl!.Steps, s => s.Label == "Net pay");
        Assert.Equal(result.NetPay, netStep.Value);
    }

    // ── Calculator-provided state steps override the generic fallback ──

    [Fact]
    public void StateExplanation_UsesCalculatorProvidedSteps_WhenAvailable()
    {
        // Pennsylvania opts in to a custom explanation, so the state line should
        // carry its flat-rate worksheet steps and reference instead of the
        // generic wage-base breakdown.
        var calc = CreateCalculator(new Core.Tax.Pennsylvania.PennsylvaniaWithholdingCalculator());

        var result = calc.Calculate(SampleInput(UsState.PA));

        var stateExpl = result.Explanation.Get(ExplanationLineKey.StateWithholding);
        Assert.NotNull(stateExpl);
        Assert.Contains(stateExpl!.Steps, s => s.Label.Contains("3.07%"));
        Assert.Contains("3.07", stateExpl.Reference);
        Assert.Equal(result.StateWithholding, stateExpl.FinalAmount);
    }

    [Fact]
    public void StateDisabilityExplanation_UsesCalculatorProvidedSteps_WhenAvailable()
    {
        // Washington's WA Cares Fund line opts in to a custom explanation.
        var calc = CreateCalculator(new Core.Tax.Washington.WashingtonWithholdingCalculator());

        var result = calc.Calculate(SampleInput(UsState.WA));

        var diExpl = result.Explanation.Get(ExplanationLineKey.StateDisability);
        Assert.NotNull(diExpl);
        Assert.Contains(diExpl!.Steps, s => s.Label.Contains("WA Cares Fund premium"));
        Assert.Equal(result.StateDisabilityInsurance, diExpl.FinalAmount);
    }

    private static PaycheckInput SampleInput(UsState state = UsState.OK) => new()
    {
        Frequency = PayFrequency.Biweekly,
        HourlyRate = 18m,
        RegularHours = 80m,
        State = state,
        FederalW4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step2Checked = true
        },
        StateInputValues = state == UsState.OK
            ? new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            }
            : new StateInputValues()
    };

    private static PayCalculator CreateCalculator(IStateWithholdingCalculator? extraCalculator = null)
    {
        var registry = new StateCalculatorRegistry();
        var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
        if (extraCalculator is not null)
            registry.Register(extraCalculator);
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
