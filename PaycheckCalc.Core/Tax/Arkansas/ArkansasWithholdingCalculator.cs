using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Arkansas;

/// <summary>
/// State module for Arkansas that wraps <see cref="ArkansasFormulaCalculator"/>
/// and exposes its inputs through the dynamic <see cref="IStateWithholdingCalculator"/> schema.
/// </summary>
public sealed class ArkansasWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly ArkansasFormulaCalculator _inner;

    public ArkansasWithholdingCalculator(ArkansasFormulaCalculator inner)
        => _inner = inner;

    public UsState State => UsState.AR;

    public IReadOnlyList<string> Validate(StateInputValues values)
        => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        int periods = context.PayPeriodsPerYear;
        var taxableWages = Math.Max(0m, context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        var withholding = _inner.CalculateWithholding(
            taxableWages,
            periods,
            exemptions);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding + additionalWithholding
        };
    }
}
