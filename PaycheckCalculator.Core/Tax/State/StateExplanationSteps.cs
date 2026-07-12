using System.Globalization;
using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Shared helpers for state calculators that opt in to "Show Your Work"
/// explanations via <see cref="StateWithholdingResult.WithholdingSteps"/>.
/// Keeps wording and number formatting consistent across states; all tax
/// math stays inside the individual calculators.
/// </summary>
internal static class StateExplanationSteps
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Formats a dollar amount the way the explanation modal expects (e.g. "$1,234.56").</summary>
    internal static string Money(decimal value) => value.ToString("C", UsCulture);

    /// <summary>Formats a fractional rate as a percentage (e.g. 0.0495m → "4.95%").</summary>
    internal static string Percent(decimal rate) => rate.ToString("P2", UsCulture);

    /// <summary>
    /// Adds the standard opening of every state explanation: gross wages and the
    /// pre-tax subtraction (only when one applies), then the "State taxable wages"
    /// step the rest of the worksheet builds on. Mirrors the generic fallback in
    /// <c>PayCalculator</c> so opted-in and fallback explanations read alike.
    /// </summary>
    internal static void AddTaxableWagesSteps(
        List<ExplanationStep> steps,
        CommonWithholdingContext context,
        decimal taxableWages)
    {
        if (context.PreTaxDeductionsReducingStateWages > 0m)
        {
            steps.Add(new ExplanationStep(
                "Gross wages this period",
                "Starting wages before state-deductible pre-tax items are removed.",
                context.GrossWages,
                $"= {Money(context.GrossWages)}"));
            steps.Add(new ExplanationStep(
                "Less pre-tax deductions reducing state wages",
                "Pre-tax items like traditional 401(k) or Section 125 medical reduce state taxable wages.",
                context.PreTaxDeductionsReducingStateWages,
                $"− {Money(context.PreTaxDeductionsReducingStateWages)}"));
        }

        steps.Add(new ExplanationStep(
            "State taxable wages",
            "The base the state's withholding formula is applied to.",
            taxableWages,
            $"= {Money(taxableWages)}"));
    }

    /// <summary>
    /// Adds the optional trailing "extra withholding" step used by states whose
    /// employee certificate allows a flat per-period add-on. No step is emitted
    /// when the employee requested nothing.
    /// </summary>
    internal static void AddExtraWithholdingStep(
        List<ExplanationStep> steps,
        decimal extraWithholding,
        decimal totalAfterExtra,
        string certificateName)
    {
        if (extraWithholding == 0m) return;

        steps.Add(new ExplanationStep(
            "Extra withholding",
            $"Additional per-period amount the employee requested on {certificateName}.",
            totalAfterExtra,
            $"+ {Money(extraWithholding)} = {Money(totalAfterExtra)}"));
    }

    /// <summary>
    /// Single-step explanation for states that levy no personal income tax.
    /// </summary>
    internal static IReadOnlyList<ExplanationStep> NoIncomeTax(Models.UsState state) =>
    [
        new ExplanationStep(
            "No state income tax",
            $"{state} does not levy a personal income tax, so no state income tax is withheld from wages.",
            0m,
            $"= {Money(0m)}")
    ];
}
