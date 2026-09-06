using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Normalized result returned by every <see cref="IStateWithholdingCalculator"/>.
/// Provides consistent output regardless of how different the state's internal
/// calculation logic may be.
/// </summary>
public sealed class StateWithholdingResult
{
    private readonly decimal _withholding;
    private readonly decimal _disabilityInsurance;
    private readonly string _disabilityInsuranceLabel = "State Disability Insurance";

    /// <summary>Wages subject to state income tax after applicable deductions.</summary>
    public decimal TaxableWages { get; init; }

    /// <summary>
    /// The jurisdiction's tax lines, when it levies more than the single
    /// income-tax-plus-assessment pair the scalar members below can express —
    /// New Jersey's SDI and FLI, or Maryland's state and county income tax.
    /// <para>
    /// When null or empty, <c>PayCalculator</c> synthesizes the lines from
    /// <see cref="Withholding"/> and <see cref="DisabilityInsurance"/>, so the
    /// states that levy one of each need not populate this.
    /// </para>
    /// </summary>
    public IReadOnlyList<StateTaxLine>? TaxLines { get; init; }

    /// <summary>
    /// State income tax withholding for the pay period — including county and
    /// local income tax where the state levies it. Derived from
    /// <see cref="TaxLines"/> when the calculator supplied them, so the scalar
    /// and itemized views can never disagree.
    /// </summary>
    public decimal Withholding
    {
        get => TaxLines is { Count: > 0 } ? SumLines(assessments: false) : _withholding;
        init => _withholding = value;
    }

    /// <summary>
    /// Employee-paid payroll assessments for the pay period (e.g. California SDI),
    /// totalled across every program the state levies. Zero when the state levies
    /// none. Derived from <see cref="TaxLines"/> when the calculator supplied them.
    /// </summary>
    public decimal DisabilityInsurance
    {
        get => TaxLines is { Count: > 0 } ? SumLines(assessments: true) : _disabilityInsurance;
        init => _disabilityInsurance = value;
    }

    /// <summary>
    /// Display label for the assessment line item. Defaults to "State Disability
    /// Insurance"; states override it (Connecticut → "Family Leave Insurance").
    /// When a state levies several programs this is the single collapsed heading —
    /// <see cref="TaxLines"/> keeps each program's own name.
    /// </summary>
    public string DisabilityInsuranceLabel
    {
        get
        {
            if (TaxLines is not { Count: > 0 })
                return _disabilityInsuranceLabel;

            var assessments = TaxLines
                .Where(line => line.Kind == StateTaxLineKind.PayrollAssessment)
                .ToList();

            return assessments.Count switch
            {
                0 => _disabilityInsuranceLabel,
                1 => assessments[0].Label,
                _ => "State Disability & Paid Leave"
            };
        }
        init => _disabilityInsuranceLabel = value;
    }

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

    private decimal SumLines(bool assessments) =>
        TaxLines!
            .Where(line => (line.Kind == StateTaxLineKind.PayrollAssessment) == assessments)
            .Sum(line => line.Amount);
}
