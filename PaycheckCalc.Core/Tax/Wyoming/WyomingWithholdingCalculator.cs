using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Wyoming;

/// <summary>
/// State module for Wyoming.  Wyoming does not levy a state individual income tax
/// and has no employee-paid state payroll assessments (state unemployment insurance
/// is funded solely by employers under Wyo. Stat. § 27-3-501 et seq.), so state
/// withholding is always zero.
///
/// This dedicated calculator mirrors the behavior of other no-income-tax states
/// (e.g., Washington) and keeps Wyoming aligned with the per-state plugin model
/// in <see cref="IStateWithholdingCalculator"/>, so any future state-specific
/// payroll contribution can be added here without reintroducing the generic
/// <see cref="NoIncomeTaxWithholdingAdapter"/>.
///
/// Source: Wyoming Department of Revenue — Wyoming has no personal income tax
/// (see Wyoming DOR "Taxation Structure" and Wyo. Const. art. 15, § 18).
/// </summary>
public sealed class WyomingWithholdingCalculator : IStateWithholdingCalculator
{
    public UsState State => UsState.WY;

    /// <summary>
    /// No required fields — validation always passes.
    /// </summary>
    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
        => new()
        {
            // Wyoming levies no state income tax on wages.
            TaxableWages = 0m,
            Withholding = 0m,
            Description = "No state income tax",
            WithholdingSteps = StateExplanationSteps.NoIncomeTax(State),
            WithholdingReference = "Wyo. Const. art. 15, § 18 — Wyoming levies no state personal income tax (2026)."
        };
}
