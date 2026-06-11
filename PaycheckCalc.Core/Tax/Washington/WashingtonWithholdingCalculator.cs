using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Washington;

/// <summary>
/// State module for Washington.  Washington levies no state individual income tax,
/// so income-tax withholding is always zero.  The calculator adds the mandatory
/// WA Cares Fund (Long-Term Care Insurance) premium at 0.58 % of all gross wages,
/// which is withheld from the employee each pay period.
///
/// Employees who hold a Department of Social and Health Services (DSHS)-approved
/// exemption certificate may opt out of the WA Cares Fund.  Set the
/// <c>WaCaresExempt</c> schema field to <c>true</c> to suppress that deduction.
///
/// Source: Washington State Department of Social and Health Services, WA Cares Fund
/// Employer Information (2026); RCW 50B.04.080.
/// </summary>
public sealed class WashingtonWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>2026 WA Cares Fund employee premium rate (0.58 %).</summary>
    private const decimal WaCaresRate = 0.0058m;

    /// <summary>Display label for the WA Cares Fund line item.</summary>
    private const string WaCaresLabel = "WA Cares Fund (Long-Term Care)";

    public UsState State => UsState.WA;

    /// <summary>
    /// No required fields — validation always passes.
    /// </summary>
    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exempt = values.GetValueOrDefault("WaCaresExempt", false);

        // WA Cares Fund: 0.58 % of ALL gross wages (no wage-base cap).
        // Applied before any pre-tax deductions — the fund does not follow
        // the same taxable-wage reductions used for income-tax purposes.
        var waCares = exempt
            ? 0m
            : Math.Round(Math.Max(0m, context.GrossWages) * WaCaresRate, 2,
                MidpointRounding.AwayFromZero);

        return new StateWithholdingResult
        {
            // Washington has no state income tax.
            TaxableWages = 0m,
            Withholding = 0m,
            DisabilityInsurance = waCares,
            DisabilityInsuranceLabel = WaCaresLabel,
            WithholdingSteps = StateExplanationSteps.NoIncomeTax(State),
            WithholdingReference = "Washington levies no state personal income tax (2026).",
            DisabilityInsuranceSteps = exempt ? null : BuildWaCaresSteps(context.GrossWages, waCares),
            DisabilityInsuranceReference = "WA Cares Fund employee premium, RCW 50B.04.080 (2026)."
        };
    }

    private static IReadOnlyList<ExplanationStep> BuildWaCaresSteps(decimal grossWages, decimal waCares) =>
    [
        new ExplanationStep(
            "Gross wages this period",
            "The WA Cares premium applies to all gross wages, before any pre-tax deductions, with no wage cap.",
            grossWages,
            $"= {StateExplanationSteps.Money(grossWages)}"),
        new ExplanationStep(
            "WA Cares Fund premium (0.58%)",
            "Mandatory long-term-care insurance premium withheld from employees unless they hold a DSHS-approved exemption.",
            waCares,
            $"{StateExplanationSteps.Money(grossWages)} × {StateExplanationSteps.Percent(WaCaresRate)} = {StateExplanationSteps.Money(waCares)}")
    ];
}
