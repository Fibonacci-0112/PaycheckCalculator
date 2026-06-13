using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.DistrictOfColumbia;

/// <summary>
/// State module for District of Columbia income tax withholding.
/// Implements the annualized percentage method per the DC Office of Tax and
/// Revenue FR-230 Income Tax Withholding Instructions and Tables.
///
/// Calculation steps (DC D-4 / percentage method):
///   1. Compute per-period taxable wages (gross − pre-tax deductions).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the standard deduction for the filing status.
///   4. Subtract the per-allowance exemption amount (D-4 line 2).
///   5. Apply graduated brackets to compute annual tax.
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested
///      (D-4 line 3).
///
/// Filing statuses (per DC Form D-4):
///   • Single                                     (standard deduction $15,000)
///   • Married/Registered Domestic Partners
///     Filing Jointly                             (standard deduction $30,000)
///   • Married/Registered Domestic Partners
///     Filing Separately                          (standard deduction $15,000)
///   • Head of Household                          (standard deduction $15,000)
///
/// 2026 annual tax brackets (same for all filing statuses — only the standard
/// deduction differs):
///        $0 –     $10,000   4.00%
///    $10,000 –    $40,000   6.00%
///    $40,000 –    $60,000   6.50%
///    $60,000 –   $250,000   8.50%
///   $250,000 –   $500,000   9.25%
///   $500,000 – $1,000,000   9.75%
///   Over  $1,000,000       10.75%
///
/// Per-allowance exemption: $1,675 annually (D-4 line 2).
///
/// Source: DC Office of Tax and Revenue FR-230 Income Tax Withholding
///         Instructions and Tables, District of Columbia Form D-4.
/// </summary>
public sealed class DistrictOfColumbiaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>
    /// Standard deduction for Single, Married Filing Separately,
    /// and Head of Household filers.
    /// </summary>
    public const decimal StandardDeductionSingle = 15_000m;

    /// <summary>
    /// Standard deduction for Married/Registered Domestic Partners Filing Jointly.
    /// </summary>
    public const decimal StandardDeductionMarriedJoint = 30_000m;

    /// <summary>
    /// Annual exemption amount per allowance claimed on DC Form D-4 (line 2).
    /// Subtracted from annual taxable wages after the standard deduction.
    /// </summary>
    public const decimal AllowanceAmount = 1_675m;

    // ── Graduated brackets (same for all filing statuses) ────────────

    private static readonly (decimal Floor, decimal? Ceiling, decimal Rate)[] Brackets =
    [
        (0m,           10_000m,    0.04m),
        (10_000m,      40_000m,    0.06m),
        (40_000m,      60_000m,    0.065m),
        (60_000m,     250_000m,    0.085m),
        (250_000m,    500_000m,    0.0925m),
        (500_000m,  1_000_000m,    0.0975m),
        (1_000_000m,  null,        0.1075m)
    ];

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarriedJoint = "Married Filing Jointly";
    public const string StatusMarriedSeparate = "Married Filing Separately";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public DistrictOfColumbiaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.DC, "FilingStatus");
    }

    public UsState State => UsState.DC;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Allowances", 0) < 0)
            errors.Add("Allowances cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

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

        // Step 3: Subtract standard deduction (only MFJ uses the $30,000 amount)
        var stdDeduction = filingStatus == StatusMarriedJoint
            ? StandardDeductionMarriedJoint
            : StandardDeductionSingle;
        annualWages -= stdDeduction;

        // Step 4: Subtract per-allowance exemption (D-4 line 2)
        annualWages -= allowances * AllowanceAmount;
        annualWages = Math.Max(0m, annualWages);

        // Step 5: Apply graduated brackets
        var annualTax = CalculateFromBrackets(annualWages);

        // Step 6: De-annualize and round
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add extra withholding (D-4 line 3)
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
