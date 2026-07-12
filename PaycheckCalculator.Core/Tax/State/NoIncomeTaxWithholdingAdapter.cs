using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Adapter for states with no individual income tax (AK, FL, NV, NH, SD, TN, TX).
/// Washington and Wyoming use dedicated calculators instead of this adapter so that
/// state-specific payroll assessments (e.g., WA Cares Fund) can live in their own modules.
/// Provides an empty input schema and always returns zero withholding.
/// </summary>
public sealed class NoIncomeTaxWithholdingAdapter : IStateWithholdingCalculator
{
    public NoIncomeTaxWithholdingAdapter(UsState state) => State = state;

    public UsState State { get; }

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
        => new()
        {
            TaxableWages = 0m,
            Withholding = 0m,
            Description = "No state income tax",
            WithholdingSteps = StateExplanationSteps.NoIncomeTax(State),
            WithholdingReference = $"{State} levies no state personal income tax (2026)."
        };
}
