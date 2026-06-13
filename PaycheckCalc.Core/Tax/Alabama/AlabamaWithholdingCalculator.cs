using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Alabama;

/// <summary>
/// State module for Alabama that exposes its unique filing statuses
/// (0, Single, Married Filing Jointly, Married Filing Separately, Head of Family)
/// and dependent count through the dynamic <see cref="IStateWithholdingCalculator"/> schema.
/// </summary>
public sealed class AlabamaWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly IReadOnlyList<string> _filingStatusOptions;

    public AlabamaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.AL, "FilingStatus");
    }

    public UsState State => UsState.AL;

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
        var filingStatusStr = values.GetValueOrDefault("FilingStatus", "Single");
        var filingStatus = MapFilingStatus(filingStatusStr);
        var dependents = values.GetValueOrDefault("Dependents", 0);
        var federalWithholding = context.FederalWithholdingPerPeriod;
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        int periods = GetPayPeriods(context.PayPeriod);
        var taxableWages = Math.Max(0m, context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        var withholding = AlabamaFormulaCalculator.CalculateWithholding(
            taxableWages,
            periods,
            federalWithholding,
            filingStatus,
            dependents);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding + additionalWithholding
        };
    }

    private static AlabamaFilingStatus MapFilingStatus(string status) => status switch
    {
        "0" => AlabamaFilingStatus.Zero,
        "Single" => AlabamaFilingStatus.Single,
        "Married Filing Jointly" => AlabamaFilingStatus.MarriedFilingJointly,
        "Married Filing Separately" => AlabamaFilingStatus.MarriedFilingSeparately,
        "Head of Family" => AlabamaFilingStatus.HeadOfFamily,
        _ => AlabamaFilingStatus.Single
    };

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => 260,
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
