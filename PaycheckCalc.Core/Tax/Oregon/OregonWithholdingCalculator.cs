using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Oregon;

/// <summary>
/// State module for Oregon (OR) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Oregon Department of Revenue employer withholding guide and
/// Form OR-W-4 (Oregon Employee's Withholding Statement and Exemption Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Low-income exemption: floor annual taxable income at zero.
///   5. Apply Oregon's 2026 graduated income tax brackets (Single for Single;
///      Married for Married and Head of Household).
///   6. Subtract the OR-W-4 allowance credit ($219 per allowance claimed).
///      Floor computed annual tax at zero (credits cannot create a refund).
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form OR-W-4.
///
/// Filing statuses (per Form OR-W-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///     HoH uses the Married rate brackets with the Single standard deduction
///     per Oregon DOR Publication 150-206-436.
///
/// 2026 Oregon amounts (Oregon DOR Publication 150-206-436):
///   Standard deduction:
///     Single / MFS:            $2,835
///     Married / QSS:           $5,670
///     Head of Household:       $2,835 (same as Single; HoH uses Married brackets)
///   Per-allowance credit (OR-W-4): $219 (applied to computed annual tax)
///   Tax brackets — Single / MFS:
///     4.75% on $0 – $4,300
///     6.75% on $4,300 – $10,750
///     8.75% on $10,750 – $125,000
///     9.9%  over $125,000
///   Tax brackets — Married / QSS and Head of Household:
///     4.75% on $0 – $8,600
///     6.75% on $8,600 – $21,500
///     8.75% on $21,500 – $250,000
///     9.9%  over $250,000
///
/// Sources:
///   • Oregon Department of Revenue, Publication 150-206-436, 2026 Oregon
///     Withholding Tax Formulas.
///   • Form OR-W-4, Oregon Employee's Withholding Statement and Exemption
///     Certificate.
/// </summary>
public sealed class OregonWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 Oregon standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 2_835m;

    /// <summary>2026 Oregon standard deduction for Married Filing Jointly / Qualifying Surviving Spouse.</summary>
    public const decimal StandardDeductionMarried = 5_670m;

    /// <summary>
    /// 2026 Oregon standard deduction for Head of Household.
    /// Oregon uses the Single standard deduction for HoH (per OR DOR Pub 150-206-436);
    /// HoH filers receive the benefit of the Married rate brackets instead.
    /// </summary>
    public const decimal StandardDeductionHeadOfHousehold = 2_835m;

    // ── Per-allowance credit ─────────────────────────────────────────

    /// <summary>
    /// Annual credit per allowance claimed on Form OR-W-4.
    /// Oregon uses $219 per allowance for tax year 2026.
    /// This credit reduces computed annual tax (not taxable income).
    /// </summary>
    public const decimal AllowanceCreditAmount = 219m;

    // ── Bracket ceiling thresholds — Single / MFS ────────────────────

    /// <summary>Upper ceiling of the first Single bracket ($4,300).</summary>
    public const decimal SingleBracket1Ceiling = 4_300m;

    /// <summary>Upper ceiling of the second Single bracket ($10,750).</summary>
    public const decimal SingleBracket2Ceiling = 10_750m;

    /// <summary>Upper ceiling of the third Single bracket ($125,000). Income over this is taxed at 9.9%.</summary>
    public const decimal SingleBracket3Ceiling = 125_000m;

    // ── Bracket ceiling thresholds — Married / QSS and Head of Household ──

    /// <summary>Upper ceiling of the first Married bracket ($8,600).</summary>
    public const decimal MarriedBracket1Ceiling = 8_600m;

    /// <summary>Upper ceiling of the second Married bracket ($21,500).</summary>
    public const decimal MarriedBracket2Ceiling = 21_500m;

    /// <summary>Upper ceiling of the third Married bracket ($250,000). Income over this is taxed at 9.9%.</summary>
    public const decimal MarriedBracket3Ceiling = 250_000m;

    // ── Tax rates (shared across all filing statuses) ────────────────

    /// <summary>Oregon rate for the first bracket (4.75%).</summary>
    public const decimal Rate1 = 0.0475m;

    /// <summary>Oregon rate for the second bracket (6.75%).</summary>
    public const decimal Rate2 = 0.0675m;

    /// <summary>Oregon rate for the third bracket (8.75%).</summary>
    public const decimal Rate3 = 0.0875m;

    /// <summary>Oregon top rate for the fourth bracket (9.9%).</summary>
    public const decimal Rate4 = 0.099m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public OregonWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.OR, "FilingStatus");
    }

public UsState State => UsState.OR;


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
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var allowances       = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the filing-status standard deduction.
        // Head of Household uses the Single standard deduction per OR DOR Pub 150-206-436.
        var standardDeduction = filingStatus switch
        {
            StatusMarried => StandardDeductionMarried,
            _             => StandardDeductionSingle   // Single and Head of Household
        };

        // Step 4: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages - standardDeduction);

        // Step 5: Apply Oregon's 2026 graduated brackets.
        // Head of Household uses the Married bracket thresholds per OR DOR Pub 150-206-436.
        var annualTax = filingStatus == StatusSingle
            ? ApplySingleBrackets(annualTaxableIncome)
            : ApplyMarriedBrackets(annualTaxableIncome);  // Married and Head of Household

        // Step 6: Subtract per-allowance credits from annual tax; floor at zero.
        annualTax = Math.Max(0m, annualTax - allowances * AllowanceCreditAmount);

        // Step 7: De-annualize and round to two decimal places.
        var periodTax   = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding  = withholding
        };
    }

    // ── Bracket helpers ───────────────────────────────────────────────

    private static decimal ApplySingleBrackets(decimal income)
    {
        // Single / MFS brackets:
        //   4.75% on $0 – $4,300
        //   6.75% on $4,300 – $10,750
        //   8.75% on $10,750 – $125,000
        //   9.9%  over $125,000
        if (income <= 0m) return 0m;
        return ComputeTax(income, SingleBracket1Ceiling, SingleBracket2Ceiling, SingleBracket3Ceiling);
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // Married / QSS and Head of Household brackets:
        //   4.75% on $0 – $8,600
        //   6.75% on $8,600 – $21,500
        //   8.75% on $21,500 – $250,000
        //   9.9%  over $250,000
        if (income <= 0m) return 0m;
        return ComputeTax(income, MarriedBracket1Ceiling, MarriedBracket2Ceiling, MarriedBracket3Ceiling);
    }

    /// <summary>
    /// Applies Oregon's four-rate bracket structure using the provided ceiling thresholds.
    /// All filing statuses share the same four rates; only the bracket thresholds differ.
    /// </summary>
    private static decimal ComputeTax(decimal income, decimal ceiling1, decimal ceiling2, decimal ceiling3)
    {
        decimal tax = 0m;

        // 4.75% on $0 – ceiling1
        tax += Math.Min(income, ceiling1) * Rate1;

        // 6.75% on ceiling1 – ceiling2
        if (income > ceiling1)
            tax += (Math.Min(income, ceiling2) - ceiling1) * Rate2;

        // 8.75% on ceiling2 – ceiling3
        if (income > ceiling2)
            tax += (Math.Min(income, ceiling3) - ceiling2) * Rate3;

        // 9.9% over ceiling3
        if (income > ceiling3)
            tax += (income - ceiling3) * Rate4;

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly    => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
