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

    // ── Taxable-income explanations (Federal / FICA / State) ──

    [Fact]
    public void Explanation_IncludesFederalFicaAndStateTaxableIncome()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.FederalTaxableIncome));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.FicaTaxableWages));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.StateTaxableWages));
    }

    [Fact]
    public void FederalTaxableIncomeExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var expl = result.Explanation.Get(ExplanationLineKey.FederalTaxableIncome);
        Assert.NotNull(expl);
        Assert.Equal(result.FederalTaxableIncome, expl!.FinalAmount);
        // No pre-tax deductions in the sample, so taxable income equals gross ($18 × 80).
        Assert.Equal(1440.00m, expl.FinalAmount);
        Assert.Contains(expl.Steps, s => s.Label == "Federal taxable income");
    }

    [Fact]
    public void FicaTaxableIncomeExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var expl = result.Explanation.Get(ExplanationLineKey.FicaTaxableWages);
        Assert.NotNull(expl);
        Assert.Equal(result.FicaTaxableWages, expl!.FinalAmount);
        Assert.Equal(1440.00m, expl.FinalAmount);
        Assert.Contains(expl.Steps, s => s.Label == "FICA taxable wages");
    }

    [Fact]
    public void StateTaxableIncomeExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var expl = result.Explanation.Get(ExplanationLineKey.StateTaxableWages);
        Assert.NotNull(expl);
        Assert.Equal(result.StateTaxableWages, expl!.FinalAmount);
        Assert.Contains(expl.Steps, s => s.Label == "State taxable wages");
    }

    [Fact]
    public void TaxableIncomeExplanations_ShowPreTaxDeductionReductions()
    {
        // 401(k) (5% = $72) reduces federal + state wages but not FICA; medical
        // ($85) reduces all three. Gross is $1,440 biweekly, so federal/state
        // taxable = $1,283 and FICA taxable = $1,355.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInputWithDeductions());

        var fed = result.Explanation.Get(ExplanationLineKey.FederalTaxableIncome);
        Assert.NotNull(fed);
        Assert.Equal(1283.00m, fed!.FinalAmount);
        Assert.Equal(result.FederalTaxableIncome, fed.FinalAmount);
        Assert.Contains(fed.Steps, s => s.Label == "Less pre-tax deductions reducing federal wages" && s.Value == 157.00m);

        var fica = result.Explanation.Get(ExplanationLineKey.FicaTaxableWages);
        Assert.NotNull(fica);
        Assert.Equal(1355.00m, fica!.FinalAmount);
        Assert.Equal(result.FicaTaxableWages, fica.FinalAmount);
        Assert.Contains(fica.Steps, s => s.Label == "Less pre-tax deductions reducing FICA wages" && s.Value == 85.00m);

        var state = result.Explanation.Get(ExplanationLineKey.StateTaxableWages);
        Assert.NotNull(state);
        Assert.Equal(1283.00m, state!.FinalAmount);
        Assert.Equal(result.StateTaxableWages, state.FinalAmount);
        Assert.Contains(state.Steps, s => s.Label == "Less pre-tax deductions reducing state wages" && s.Value == 157.00m);
    }

    [Fact]
    public void StateTaxableIncomeExplanation_NoIncomeTaxState_ShowsZeroWageBase()
    {
        // Texas levies no state income tax: taxable wages are zero even though
        // gross pay is positive, so the line shows a "No state taxable wages" note.
        var calc = CreateCalculator(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var result = calc.Calculate(SampleInput(UsState.TX));

        var state = result.Explanation.Get(ExplanationLineKey.StateTaxableWages);
        Assert.NotNull(state);
        Assert.Equal(0m, state!.FinalAmount);
        Assert.Equal(result.StateTaxableWages, state.FinalAmount);
        Assert.Contains(state.Steps, s => s.Label == "No state taxable wages");
    }

    // ── Sources aggregation ──

    [Fact]
    public void Sources_ExcludesLinesWithoutReference()
    {
        // GrossPay and NetPay explanations don't carry a Reference citation, so
        // they must not appear in the aggregated Sources list.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.DoesNotContain(result.Explanation.Sources, s => s.Label == "Gross Pay");
        Assert.DoesNotContain(result.Explanation.Sources, s => s.Label == "Net Pay");
    }

    [Fact]
    public void Sources_IncludesFederalFicaAndStateCitations()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var federalExpl = result.Explanation.Get(ExplanationLineKey.FederalWithholding)!;
        var ssExpl = result.Explanation.Get(ExplanationLineKey.SocialSecurity)!;
        var medicareExpl = result.Explanation.Get(ExplanationLineKey.Medicare)!;
        var stateExpl = result.Explanation.Get(ExplanationLineKey.StateWithholding)!;

        Assert.NotEmpty(federalExpl.Reference);
        Assert.NotEmpty(ssExpl.Reference);
        Assert.NotEmpty(medicareExpl.Reference);
        Assert.NotEmpty(stateExpl.Reference);

        Assert.Contains(result.Explanation.Sources, s => s.Label == federalExpl.Title && s.Reference == federalExpl.Reference);
        Assert.Contains(result.Explanation.Sources, s => s.Label == ssExpl.Title && s.Reference == ssExpl.Reference);
        Assert.Contains(result.Explanation.Sources, s => s.Label == medicareExpl.Title && s.Reference == medicareExpl.Reference);
        Assert.Contains(result.Explanation.Sources, s => s.Label == stateExpl.Title && s.Reference == stateExpl.Reference);
    }

    [Fact]
    public void Sources_EmptyExplanation_ProducesNoSources()
    {
        Assert.Empty(PaycheckExplanation.Empty.Sources);
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

    private static PaycheckInput SampleInputWithDeductions() => new()
    {
        Frequency = PayFrequency.Biweekly,
        HourlyRate = 18m,
        RegularHours = 80m,
        State = UsState.OK,
        FederalW4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step2Checked = true
        },
        StateInputValues = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        },
        Deductions = new[]
        {
            new Deduction
            {
                Name = "401(k)",
                Type = DeductionType.PreTax,
                Amount = 5m,
                AmountType = DeductionAmountType.Percentage,
                ReducesFederalTaxableWages = true,
                ReducesStateTaxableWages = true,
                ReducesFicaWages = false
            },
            new Deduction
            {
                Name = "Medical",
                Type = DeductionType.PreTax,
                Amount = 85m,
                AmountType = DeductionAmountType.Dollar,
                ReducesFederalTaxableWages = true,
                ReducesStateTaxableWages = true,
                ReducesFicaWages = true
            }
        }
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
