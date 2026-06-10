namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Normalized result returned by every <see cref="IStateWithholdingCalculator"/>.
/// Provides consistent output regardless of how different the state's internal
/// calculation logic may be.
/// </summary>
public sealed class StateWithholdingResult
{
    /// <summary>Wages subject to state income tax after applicable deductions.</summary>
    public decimal TaxableWages { get; init; }

    /// <summary>State income tax withholding for the pay period.</summary>
    public decimal Withholding { get; init; }

    /// <summary>
    /// State disability insurance withholding for the pay period (e.g., California SDI).
    /// Zero when the state does not impose disability insurance.
    /// </summary>
    public decimal DisabilityInsurance { get; init; }

    /// <summary>
    /// Display label for the disability-insurance line item.
    /// Defaults to "State Disability Insurance" when not set by the calculator.
    /// States may override (e.g., Connecticut → "Family Leave Insurance").
    /// </summary>
    public string DisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    /// <summary>
    /// Optional human-readable note (e.g., "Exempt — no tax due",
    /// "Includes supplemental surcharge").
    /// </summary>
    public string? Description { get; init; }
}
