using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Kansas;

/// <summary>
/// State module for Kansas.  Two-bracket graduated income tax applied to
/// annualized taxable wages after a filing-status standard deduction and
/// K-4 allowance subtractions.
///
/// Sources:
///   • Kansas Department of Revenue — Kansas Withholding Tax Guide for 2026.
///   • Kansas Form K-4 "Kansas Employee's Withholding Allowance Certificate"
///     — single/married filing status, allowances at $2,250 each, and an
///     optional additional per-period withholding line.
///
/// 2026 rate schedule (annualized taxable income):
///   Single:
///     $0–$23,000  →  5.20%
///     Over $23,000 →  5.58%
///   Married:
///     $0–$46,000  →  5.20%
///     Over $46,000 →  5.58%
///
/// Standard deduction:
///   Single — $3,605
///   Married — $8,240
///
/// Allowance: $2,250 per allowance claimed on K-4.
///
/// Calculation steps:
///   1. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   2. Annualize taxable wages by multiplying by pay periods per year.
///   3. Subtract the standard deduction for the filing status.
///   4. Subtract the allowance deduction (allowances × $2,250).
///   5. Floor the result at $0.
///   6. Apply the graduated brackets to compute annual tax.
///   7. De-annualize: per-period tax = annual tax ÷ pay periods,
///      rounded to two decimal places.
///   8. Add any extra per-period withholding from K-4.
/// </summary>
public sealed class KansasWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Annual standard deduction for Single filers (2026).</summary>
    private const decimal StandardDeductionSingle = 3_605m;

    /// <summary>Annual standard deduction for Married filers (2026).</summary>
    private const decimal StandardDeductionMarried = 8_240m;

    /// <summary>Annual deduction per K-4 allowance claimed (2026).</summary>
    private const decimal AllowanceAmount = 2_250m;

    /// <summary>Lower bracket ceiling for Single filers.</summary>
    private const decimal BracketThresholdSingle = 23_000m;

    /// <summary>Lower bracket ceiling for Married filers.</summary>
    private const decimal BracketThresholdMarried = 46_000m;

    /// <summary>Rate applied to income at or below the bracket threshold (5.20%).</summary>
    private const decimal LowerRate = 0.052m;

    /// <summary>Rate applied to income above the bracket threshold (5.58%).</summary>
    private const decimal UpperRate = 0.0558m;

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public KansasWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.KS, "FilingStatus");
    }

    public UsState State => UsState.KS;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        var allowances = values.GetValueOrDefault("Allowances", 0);
        if (allowances < 0)
            errors.Add("K-4 Allowances cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var isMarried = values.GetValueOrDefault("FilingStatus", "Single") == "Married";
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 1: State taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize.
        decimal annualWages = taxableWages * periods;

        // Step 3: Subtract filing-status standard deduction.
        annualWages -= isMarried ? StandardDeductionMarried : StandardDeductionSingle;

        // Step 4: Subtract K-4 allowance deductions.
        annualWages -= allowances * AllowanceAmount;

        // Step 5: Floor at $0.
        annualWages = Math.Max(0m, annualWages);

        // Step 6: Apply graduated brackets.
        decimal bracketThreshold = isMarried ? BracketThresholdMarried : BracketThresholdSingle;
        decimal annualTax;
        if (annualWages <= bracketThreshold)
        {
            annualTax = annualWages * LowerRate;
        }
        else
        {
            annualTax = (bracketThreshold * LowerRate)
                      + ((annualWages - bracketThreshold) * UpperRate);
        }

        // Step 7: De-annualize and round to cents.
        decimal withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add extra per-pay withholding from K-4.
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
