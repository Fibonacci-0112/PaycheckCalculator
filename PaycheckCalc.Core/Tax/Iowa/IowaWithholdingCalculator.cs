using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Iowa;

/// <summary>
/// State module for Iowa.  Flat state income tax applied to gross taxable
/// wages (annualized percentage method with no standard deduction and no
/// per-allowance subtraction).
///
/// Source: Iowa Department of Revenue withholding guidance for 2026; the
/// withholding rate matches the rate previously carried by this state in
/// <see cref="StateTaxConfigs2026"/> (3.65%).  Iowa's individual income tax
/// was collapsed to a flat rate by HF 2317; the withholding formula follows
/// the same flat-rate structure.
///
/// Calculation steps:
///   1. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   2. Annualize the taxable wages by multiplying by pay periods per year.
///   3. Annual withholding = annual taxable wages × 3.65%.
///   4. Per-period withholding = annual withholding ÷ pay periods per year,
///      rounded to two decimal places.
///   5. Add any extra per-period withholding the employee requested on
///      IA W-4 Line 6.
/// </summary>
public sealed class IowaWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Iowa flat state income tax withholding rate for 2026 (3.65%).</summary>
    private const decimal FlatRate = 0.0365m;

    public UsState State => UsState.IA;

    public IReadOnlyList<string> Validate(StateInputValues values) => Array.Empty<string>();

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 1: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Steps 2–3: Annualize and apply the flat rate.
        decimal annualTax = taxableWages * periods * FlatRate;

        // Step 4: Per-period withholding, rounded to cents.
        decimal withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        // Step 5: Add extra per-pay withholding from IA W-4 Line 6.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => 260,
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
