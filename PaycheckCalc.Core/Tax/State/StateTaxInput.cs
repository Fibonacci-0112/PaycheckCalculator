using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Generic input passed to every state tax calculator.
/// </summary>
public sealed class StateTaxInput
{
    public decimal GrossWages { get; init; }
    public PayFrequency Frequency { get; init; }

    /// <summary>
    /// Resolved number of pay periods in the year (accounting for 53-week / 27-biweekly
    /// years). When null, the calculator falls back to the fixed count for <see cref="Frequency"/>.
    /// </summary>
    public int? PayPeriodsPerYear { get; init; }

    public FilingStatus FilingStatus { get; init; }
    public int Allowances { get; init; }
    public decimal AdditionalWithholding { get; init; }

    /// <summary>
    /// Sum of pre-tax deductions that reduce state taxable wages.
    /// </summary>
    public decimal PreTaxDeductionsReducingStateWages { get; init; }
}
