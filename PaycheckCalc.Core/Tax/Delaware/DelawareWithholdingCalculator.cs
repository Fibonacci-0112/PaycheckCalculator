using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Delaware;

/// <summary>
/// State module for Delaware income tax withholding.
/// Implements the annualized percentage method per the Delaware Division of Revenue
/// Employer's Guide to Withholding Tables.
///
/// Calculation steps (DE W-4 / percentage method):
///   1. Compute per-period taxable wages (gross − pre-tax deductions).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the standard deduction for the filing status.
///   4. Apply graduated brackets to compute annual tax.
///   5. Subtract personal credits ($110 × allowances claimed on DE W-4).
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested.
///
/// Filing statuses (per DE W-4):
///   • Single
///   • Married Filing Jointly (standard deduction $6,500)
///   • Married Filing Separately (standard deduction $3,250)
///   • Head of Household (standard deduction $3,250)
///
/// 2026 tax brackets (same for all filing statuses — only the
/// standard deduction differs):
///   $0 –  $2,000     0.0%
///   $2,001 –  $5,000  2.2%
///   $5,001 – $10,000  3.9%
///   $10,001 – $20,000 4.8%
///   $20,001 – $25,000 5.2%
///   $25,001 – $60,000 5.55%
///   Over $60,000      6.6%
///
/// Source: Delaware Division of Revenue Employer's Guide (Withholding Tables),
///         <see href="https://revenue.delaware.gov/employers-guide-withholding-tables/"/>
/// </summary>
public sealed class DelawareWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>Standard deduction for Single, Married Filing Separately, and Head of Household.</summary>
    private const decimal StandardDeductionSingle = 3_250m;

    /// <summary>Standard deduction for Married Filing Jointly.</summary>
    private const decimal StandardDeductionMarriedJoint = 6_500m;

    /// <summary>
    /// Annual tax credit per personal exemption claimed on DE W-4.
    /// Subtracted from computed annual tax (not from taxable income).
    /// </summary>
    private const decimal PersonalCreditPerAllowance = 110m;

    // ── Graduated brackets (same for all filing statuses) ────────────

    private static readonly (decimal Floor, decimal? Ceiling, decimal Rate)[] Brackets =
    [
        (0m,      2_000m,  0m),
        (2_000m,  5_000m,  0.022m),
        (5_000m,  10_000m, 0.039m),
        (10_000m, 20_000m, 0.048m),
        (20_000m, 25_000m, 0.052m),
        (25_000m, 60_000m, 0.0555m),
        (60_000m, null,    0.066m)
    ];

    // ── Filing status constants ──────────────────────────────────────

    private const string StatusSingle = "Single";
    private const string StatusMarriedJoint = "Married Filing Jointly";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public DelawareWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.DE, "FilingStatus");
    }

    public UsState State => UsState.DE;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        // Step 1: Per-period taxable wages
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize
        var annualWages = taxableWages * periods;

        // Step 3: Subtract standard deduction
        var stdDeduction = filingStatus == StatusMarriedJoint
            ? StandardDeductionMarriedJoint
            : StandardDeductionSingle;
        annualWages -= stdDeduction;
        annualWages = Math.Max(0m, annualWages);

        // Step 4: Apply graduated brackets
        var annualTax = CalculateFromBrackets(annualWages);

        // Step 5: Subtract personal credits ($110 per allowance on DE W-4)
        annualTax -= allowances * PersonalCreditPerAllowance;
        annualTax = Math.Max(0m, annualTax);

        // Step 6: De-annualize and round
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add extra withholding
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static decimal CalculateFromBrackets(decimal income)
    {
        decimal tax = 0m;
        foreach (var (floor, ceiling, rate) in Brackets)
        {
            if (income <= floor)
                break;

            var bracketCeiling = ceiling ?? decimal.MaxValue;
            var taxableInBracket = Math.Min(income, bracketCeiling) - floor;
            tax += taxableInBracket * rate;
        }

        return tax;
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
