using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Illinois;

/// <summary>
/// State module for Illinois.  Flat 4.95% income tax with allowance-based
/// exemptions from the IL-W-4.
///
/// Calculation steps:
///   1. Compute annual exemption = (Basic Allowances × $2,925) + (Additional Allowances × $1,000).
///   2. Per-period exemption = annual exemption ÷ number of pay periods per year.
///   3. Taxable wages per period = gross wages − pre-tax deductions − per-period exemption (floored at 0).
///   4. Withholding = taxable wages × 4.95%, rounded to two decimal places.
///   5. Add any extra withholding the employee requested.
/// </summary>
public sealed class IllinoisWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Illinois flat income tax rate (4.95%).</summary>
    private const decimal FlatRate = 0.0495m;

    /// <summary>Annual exemption per basic allowance (IL-W-4 Line 1).</summary>
    private const decimal BasicAllowanceAmount = 2_925m;

    /// <summary>Annual exemption per additional allowance (IL-W-4 Line 2).</summary>
    private const decimal AdditionalAllowanceAmount = 1_000m;

    public UsState State => UsState.IL;

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var basicAllowances = values.GetValueOrDefault("BasicAllowances", 0);
        var additionalAllowances = values.GetValueOrDefault("AdditionalAllowances", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = context.PayPeriodsPerYear;

        var steps = new List<ExplanationStep>();
        StateExplanationSteps.AddTaxableWagesSteps(steps, context, taxableWages);

        // Step 1: Annual exemption from IL-W-4 allowances
        decimal annualExemption = (basicAllowances * BasicAllowanceAmount)
                                + (additionalAllowances * AdditionalAllowanceAmount);

        // Step 2: Per-period exemption
        decimal exemptionPerPeriod = annualExemption / periods;

        // Step 3: Taxable amount after exemptions (floored at zero)
        decimal taxableAmount = Math.Max(0m, taxableWages - exemptionPerPeriod);

        if (annualExemption > 0m)
        {
            steps.Add(new ExplanationStep(
                "Annual exemption from IL-W-4 allowances",
                $"Each basic allowance (Line 1) is worth {StateExplanationSteps.Money(BasicAllowanceAmount)} per year and each additional allowance (Line 2) {StateExplanationSteps.Money(AdditionalAllowanceAmount)}.",
                annualExemption,
                $"({basicAllowances} × {StateExplanationSteps.Money(BasicAllowanceAmount)}) + ({additionalAllowances} × {StateExplanationSteps.Money(AdditionalAllowanceAmount)}) = {StateExplanationSteps.Money(annualExemption)}"));
            steps.Add(new ExplanationStep(
                $"Per-period exemption ({periods} pay periods/year)",
                "The annual exemption is spread evenly across the year's pay periods.",
                exemptionPerPeriod,
                $"{StateExplanationSteps.Money(annualExemption)} ÷ {periods} = {StateExplanationSteps.Money(exemptionPerPeriod)}"));
            steps.Add(new ExplanationStep(
                "Wages subject to tax after exemption",
                "Taxable wages less the per-period exemption, floored at zero.",
                taxableAmount,
                $"max(0, {StateExplanationSteps.Money(taxableWages)} − {StateExplanationSteps.Money(exemptionPerPeriod)}) = {StateExplanationSteps.Money(taxableAmount)}"));
        }

        // Step 4: Flat 4.95% withholding
        decimal withholding = Math.Round(taxableAmount * FlatRate, 2, MidpointRounding.AwayFromZero);

        steps.Add(new ExplanationStep(
            "Withholding at Illinois' flat rate (4.95%)",
            "Illinois taxes all income at a single flat rate.",
            withholding,
            $"{StateExplanationSteps.Money(taxableAmount)} × {StateExplanationSteps.Percent(FlatRate)} = {StateExplanationSteps.Money(withholding)}"));

        // Step 5: Add extra withholding
        withholding += extraWithholding;
        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "Form IL-W-4, Line 3");

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            WithholdingSteps = steps,
            WithholdingReference = "Illinois DOR Booklet IL-700-T, 2026 — flat 4.95% with IL-W-4 allowances."
        };
    }
}
