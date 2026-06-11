using PaycheckCalc.Core.Explanation;

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

    /// <summary>
    /// Optional worksheet-style steps showing how <see cref="Withholding"/> was
    /// computed. Calculators opt in by supplying the full narrative (starting
    /// from gross/taxable wages); when null or empty, <c>PayCalculator</c> falls
    /// back to a generic wage-base + final-amount breakdown.
    /// </summary>
    public IReadOnlyList<ExplanationStep>? WithholdingSteps { get; init; }

    /// <summary>
    /// Optional citation for <see cref="WithholdingSteps"/>
    /// (e.g., "Illinois Booklet IL-700-T (2026)").
    /// </summary>
    public string? WithholdingReference { get; init; }

    /// <summary>
    /// Optional worksheet-style steps for the <see cref="DisabilityInsurance"/>
    /// line (e.g., California SDI, WA Cares Fund). When null or empty,
    /// <c>PayCalculator</c> falls back to a one-line generic breakdown.
    /// </summary>
    public IReadOnlyList<ExplanationStep>? DisabilityInsuranceSteps { get; init; }

    /// <summary>
    /// Optional citation for <see cref="DisabilityInsuranceSteps"/>
    /// (e.g., "California EDD DE 44 (2026) — SDI").
    /// </summary>
    public string? DisabilityInsuranceReference { get; init; }
}
