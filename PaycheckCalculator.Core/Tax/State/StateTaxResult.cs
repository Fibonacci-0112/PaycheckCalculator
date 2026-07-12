namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Result returned by every state tax calculator.
/// </summary>
public sealed class StateTaxResult
{
    public decimal TaxableWages { get; init; }
    public decimal Withholding { get; init; }
}
