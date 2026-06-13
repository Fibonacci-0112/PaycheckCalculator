using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Kentucky;

/// <summary>
/// State module for Kentucky.  Flat 4.0% income tax with a standard
/// deduction and K-4 allowance credits per the Kentucky Department of
/// Revenue 2026 withholding formula (Form 42A003).
///
/// Sources:
///   • Kentucky Department of Revenue, Form 42A003,
///     "Kentucky Withholding Tax Formula" (effective January 1, 2026):
///     flat rate 4.0%, standard deduction $3,160, $10 per-allowance credit.
///   • Kentucky Form K-4, "Kentucky's Withholding Certificate" — employees
///     claim withholding allowances; each allowance equals a $10 annual tax
///     credit subtracted from computed withholding.  Unlike many states,
///     the K-4 allowance is a credit against tax, not a deduction from income.
///
/// Calculation steps (Form 42A003 annualized percentage method):
///   1. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   2. Annualize: annual wages = taxable wages × pay periods per year.
///   3. Subtract the standard deduction ($3,160) from annual wages.
///      Annual taxable income = max(0, annual wages − $3,160).
///   4. Compute annual tax = annual taxable income × 4.0%.
///   5. Compute annual K-4 credit = allowances × $10.
///   6. Annual withholding = max(0, annual tax − annual K-4 credit).
///   7. Per-period withholding = annual withholding ÷ pay periods per year,
///      rounded to two decimal places.
///   8. Add any extra per-period withholding the employee requested on
///      K-4 Line 3.
///
/// Filing status does not affect the Kentucky calculation; the standard
/// deduction amount is uniform across all filers.
/// </summary>
public sealed class KentuckyWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Kentucky flat income tax withholding rate for 2026 (4.0%).</summary>
    private const decimal FlatRate = 0.04m;

    /// <summary>
    /// Annual standard deduction (Form 42A003, 2026): $3,160.
    /// Uniform for all filing statuses.
    /// </summary>
    private const decimal StandardDeduction = 3_160m;

    /// <summary>
    /// Annual K-4 credit per withholding allowance (Form 42A003, 2026): $10.
    /// This is a tax credit, not an income deduction.
    /// </summary>
    private const decimal AllowanceCreditAmount = 10m;

    public UsState State => UsState.KY;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var allowances = values.GetValueOrDefault("Allowances", 0);
        if (allowances < 0)
            errors.Add("K-4 Allowances cannot be negative.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 1: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize per-period taxable wages.
        decimal annualWages = taxableWages * periods;

        // Step 3: Subtract $3,160 standard deduction; floor at zero.
        decimal annualTaxableIncome = Math.Max(0m, annualWages - StandardDeduction);

        // Step 4: Annual tax at flat 4.0%.
        decimal annualTax = annualTaxableIncome * FlatRate;

        // Step 5: Annual K-4 credit ($10 per allowance).
        decimal annualCredit = allowances * AllowanceCreditAmount;

        // Step 6: Annual withholding after credit (floored at zero).
        decimal annualWithholding = Math.Max(0m, annualTax - annualCredit);

        // Step 7: Per-period withholding, rounded to cents.
        decimal withholding = Math.Round(annualWithholding / periods, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add extra per-pay withholding from K-4 Line 3.
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
