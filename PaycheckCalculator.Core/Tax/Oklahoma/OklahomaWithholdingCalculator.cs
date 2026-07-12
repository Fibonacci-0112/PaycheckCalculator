using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.Oklahoma;

/// <summary>
/// State module for Oklahoma that wraps the OW-2 percentage calculator
/// and exposes its inputs through the dynamic schema.
/// </summary>
public sealed class OklahomaWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly OklahomaOw2PercentageCalculator _inner;
    private readonly IReadOnlyList<string> _filingStatusOptions;

    public OklahomaWithholdingCalculator(OklahomaOw2PercentageCalculator inner, IStateSchemaProvider schemaProvider)
    {
        _inner = inner;
        _filingStatusOptions = schemaProvider.GetOptions(UsState.OK, "FilingStatus");
    }

    public UsState State => UsState.OK;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", "Single") == "Married"
            ? FilingStatus.Married
            : FilingStatus.Single;
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var allowancesTotal = allowances * _inner.GetAllowanceAmount(context.PayPeriod);
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages - allowancesTotal);
        var withholding = _inner.CalculateWithholding(taxableWages, context.PayPeriod, filingStatus)
                        + additionalWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
