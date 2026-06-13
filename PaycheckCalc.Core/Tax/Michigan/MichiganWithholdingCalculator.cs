using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Michigan;

/// <summary>
/// State module for Michigan.  Flat 4.25% income tax with a personal/dependent
/// exemption-based subtraction from the MI-W4.
///
/// Source: Michigan Department of Treasury, Form 446, "2026 Michigan Income
/// Tax Withholding Guide" — the 2026 flat rate is 4.25% and each MI-W4
/// exemption is worth $5,900 per year.  Michigan does not publish separate
/// single/married withholding tables; filing status does not affect the
/// per-period calculation, only exemptions do.
///
/// Calculation steps (Form 446, "Computing Michigan Withholding – Percentage
/// Formula Method"):
///   1. Annual exemption allowance = Exemptions × $5,900.
///   2. Per-period exemption allowance = annual exemption ÷ pay periods per year.
///   3. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   4. Taxable amount = max(0, state taxable wages − per-period exemption).
///   5. Withholding = taxable amount × 4.25%, rounded to two decimal places.
///   6. Add any extra per-period withholding the employee requested on
///      MI-W4, Line 6.
/// </summary>
public sealed class MichiganWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Michigan flat income tax rate for 2026 (4.25%).</summary>
    private const decimal FlatRate = 0.0425m;

    /// <summary>Annual value of one MI-W4 exemption for 2026.</summary>
    private const decimal ExemptionAmount = 5_900m;

    public UsState State => UsState.MI;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        if (exemptions < 0)
            errors.Add("MI-W4 Exemptions cannot be negative.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 3: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = context.PayPeriodsPerYear;

        var steps = new List<ExplanationStep>();
        StateExplanationSteps.AddTaxableWagesSteps(steps, context, taxableWages);

        // Step 1: Annual exemption allowance.
        decimal annualExemption = exemptions * ExemptionAmount;

        // Step 2: Per-period exemption allowance.
        decimal exemptionPerPeriod = annualExemption / periods;

        // Step 4: Taxable amount after exemptions (floored at zero).
        decimal taxableAmount = Math.Max(0m, taxableWages - exemptionPerPeriod);

        if (annualExemption > 0m)
        {
            steps.Add(new ExplanationStep(
                "Annual exemption allowance (MI-W4)",
                $"Each exemption claimed on the MI-W4 is worth {StateExplanationSteps.Money(ExemptionAmount)} per year.",
                annualExemption,
                $"{exemptions} × {StateExplanationSteps.Money(ExemptionAmount)} = {StateExplanationSteps.Money(annualExemption)}"));
            steps.Add(new ExplanationStep(
                $"Per-period exemption ({periods} pay periods/year)",
                "The annual exemption allowance is spread evenly across the year's pay periods.",
                exemptionPerPeriod,
                $"{StateExplanationSteps.Money(annualExemption)} ÷ {periods} = {StateExplanationSteps.Money(exemptionPerPeriod)}"));
            steps.Add(new ExplanationStep(
                "Wages subject to tax after exemption",
                "Taxable wages less the per-period exemption, floored at zero.",
                taxableAmount,
                $"max(0, {StateExplanationSteps.Money(taxableWages)} − {StateExplanationSteps.Money(exemptionPerPeriod)}) = {StateExplanationSteps.Money(taxableAmount)}"));
        }

        // Step 5: Flat 4.25% withholding, rounded to cents.
        decimal withholding = Math.Round(taxableAmount * FlatRate, 2, MidpointRounding.AwayFromZero);

        steps.Add(new ExplanationStep(
            "Withholding at Michigan's flat rate (4.25%)",
            "Michigan taxes all income at a single flat rate; filing status does not change the per-period amount.",
            withholding,
            $"{StateExplanationSteps.Money(taxableAmount)} × {StateExplanationSteps.Percent(FlatRate)} = {StateExplanationSteps.Money(withholding)}"));

        // Step 6: Add extra withholding from MI-W4, Line 6.
        withholding += extraWithholding;
        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "MI-W4, Line 6");

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            WithholdingSteps = steps,
            WithholdingReference = "Michigan Dept. of Treasury Form 446 (2026) — flat 4.25% with MI-W4 exemptions."
        };
    }

}
