using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Pennsylvania;

/// <summary>
/// State module for Pennsylvania (flat 3.07% rate).
/// Filing status and allowances do not affect Pennsylvania withholding,
/// so the schema contains only an optional extra withholding field.
/// </summary>
public sealed class PennsylvaniaWithholdingCalculator : IStateWithholdingCalculator
{
    private const decimal FlatRate = 0.0307m;

    public UsState State => UsState.PA;

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);
        var baseWithholding = Math.Round(taxableWages * FlatRate, 2, MidpointRounding.AwayFromZero);
        var withholding = baseWithholding + extraWithholding;

        var steps = new List<ExplanationStep>();
        StateExplanationSteps.AddTaxableWagesSteps(steps, context, taxableWages);
        steps.Add(new ExplanationStep(
            "Withholding at Pennsylvania's flat rate (3.07%)",
            "Pennsylvania taxes all compensation at a single flat rate; filing status and allowances do not change the amount.",
            baseWithholding,
            $"{StateExplanationSteps.Money(taxableWages)} × {StateExplanationSteps.Percent(FlatRate)} = {StateExplanationSteps.Money(baseWithholding)}"));
        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "the employee's withholding request");

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            WithholdingSteps = steps,
            WithholdingReference = "Pennsylvania DOR Employer Withholding Guide (REV-415), 2026 — flat 3.07% rate."
        };
    }
}
