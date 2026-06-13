using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Generic input passed to every state tax calculator.
/// </summary>
public sealed class StateTaxInput
{
    public decimal GrossWages { get; init; }
    public PayFrequency Frequency { get; init; }
    public FilingStatus FilingStatus { get; init; }
    public int Allowances { get; init; }
    public decimal AdditionalWithholding { get; init; }

    /// <summary>
    /// Sum of pre-tax deductions that reduce state taxable wages.
    /// </summary>
    public decimal PreTaxDeductionsReducingStateWages { get; init; }
}
