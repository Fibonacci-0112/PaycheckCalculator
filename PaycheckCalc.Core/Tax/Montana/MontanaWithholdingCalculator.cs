using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Montana;

/// <summary>
/// State module for Montana (MT) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Montana Department of Revenue Withholding Tax Guide and Form MW-4
/// (Employee's Withholding Allowance and Exemption Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Compute the variable standard deduction: 20% of annual wages,
///      bounded by the filing-status minimum and maximum.
///   4. Subtract the standard deduction and the MW-4 exemption deduction
///      ($3,040 per exemption claimed).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply Montana's 2026 graduated income tax brackets.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form MW-4.
///
/// Filing statuses (per Form MW-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — uses the married standard-deduction limits per the
///                         Montana DOR withholding instructions.
///
/// 2026 Montana amounts (Montana DOR Withholding Tax Guide):
///   Standard deduction: 20% of annual wages
///     Single / MFS:         minimum $4,370 / maximum $5,310
///     Married / HoH:        minimum $8,740 / maximum $10,620
///   Per-exemption deduction (MW-4): $3,040
///   Tax brackets (all filing statuses):
///     4.7% on $0 – $23,800
///     5.9% over $23,800
///
/// Sources:
///   • Montana Department of Revenue, Montana Withholding Tax Guide, 2026.
///   • Form MW-4, Employee's Withholding Allowance and Exemption Certificate.
///   • Montana HB 192 (2021) and SB 399 (2023): rate reductions to current levels.
/// </summary>
public sealed class MontanaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deduction constants ─────────────────────────────────

    /// <summary>Standard deduction rate applied to annual wages (20%).</summary>
    public const decimal StandardDeductionRate = 0.20m;

    /// <summary>Minimum standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingleMin = 4_370m;

    /// <summary>Maximum standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingleMax = 5_310m;

    /// <summary>Minimum standard deduction for Married / Head of Household filers.</summary>
    public const decimal StandardDeductionMarriedMin = 8_740m;

    /// <summary>Maximum standard deduction for Married / Head of Household filers.</summary>
    public const decimal StandardDeductionMarriedMax = 10_620m;

    // ── Per-exemption deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per personal exemption claimed on Form MW-4.
    /// Montana uses $3,040 per exemption for tax year 2026.
    /// </summary>
    public const decimal ExemptionAmount = 3_040m;

    // ── Bracket constants ────────────────────────────────────────────

    /// <summary>Upper bound of the lower bracket. Income over this is taxed at the higher rate.</summary>
    public const decimal BracketCeiling = 23_800m;

    /// <summary>Montana rate for income in the first bracket (4.7% on $0 – $23,800).</summary>
    public const decimal RateLow = 0.047m;

    /// <summary>Montana top rate for income over $23,800 (5.9%).</summary>
    public const decimal RateHigh = 0.059m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MontanaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MT, "FilingStatus");
    }

    public UsState State => UsState.MT;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Exemptions", 0) < 0)
            errors.Add("Exemptions cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var exemptions       = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        bool isMarriedOrHoH = filingStatus == StatusMarried
                           || filingStatus == StatusHeadOfHousehold;

        // Step 3: Variable standard deduction — 20% of annual wages, bounded
        //         by the filing-status minimum and maximum per the MT guide.
        //         Head of Household uses the married limits.
        var (stdMin, stdMax) = isMarriedOrHoH
            ? (StandardDeductionMarriedMin, StandardDeductionMarriedMax)
            : (StandardDeductionSingleMin,  StandardDeductionSingleMax);

        var standardDeduction = Math.Max(stdMin,
            Math.Min(annualWages * StandardDeductionRate, stdMax));

        // Step 4: Subtract the MW-4 exemption deduction ($3,040 per exemption).
        var exemptionDeduction = exemptions * ExemptionAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - exemptionDeduction);

        // Step 6: Apply Montana's 2026 brackets.
        //   4.7% on $0 – $23,800
        //   5.9% over $23,800
        var annualTax = ApplyBrackets(annualTaxableIncome);

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

    // ── Bracket helper ────────────────────────────────────────────────

    private static decimal ApplyBrackets(decimal income)
    {
        // Montana uses the same two brackets for all filing statuses.
        // Only the standard deduction and its min/max limits differ by status.
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        // 4.7% on $0 – $23,800
        tax += Math.Min(income, BracketCeiling) * RateLow;

        // 5.9% over $23,800
        if (income > BracketCeiling)
            tax += (income - BracketCeiling) * RateHigh;

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly   => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
