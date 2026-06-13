using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Adapts <see cref="PercentageMethodStateTaxCalculator"/> to the
/// <see cref="IStateWithholdingCalculator"/> interface, providing
/// a standard schema for the ~39 states that use the annualized percentage method.
/// </summary>
public sealed class PercentageMethodWithholdingAdapter : IStateWithholdingCalculator
{
    private readonly PercentageMethodStateTaxCalculator _inner;
    private readonly IReadOnlyList<string> _filingStatusOptions;

    public PercentageMethodWithholdingAdapter(UsState state, PercentageMethodConfig config, IStateSchemaProvider schemaProvider)
    {
        _inner = new PercentageMethodStateTaxCalculator(state, config);
        _filingStatusOptions = schemaProvider.GetOptions(state, "FilingStatus");
    }

    public UsState State => _inner.State;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (_filingStatusOptions.Count > 0 && !_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", "Single") == "Married"
            ? FilingStatus.Married
            : FilingStatus.Single;

        var result = _inner.CalculateWithholding(new StateTaxInput
        {
            GrossWages = context.GrossWages,
            Frequency = context.PayPeriod,
            FilingStatus = filingStatus,
            Allowances = values.GetValueOrDefault("Allowances", 0),
            AdditionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m),
            PreTaxDeductionsReducingStateWages = context.PreTaxDeductionsReducingStateWages
        });

        return new StateWithholdingResult
        {
            TaxableWages = result.TaxableWages,
            Withholding = result.Withholding
        };
    }
}
