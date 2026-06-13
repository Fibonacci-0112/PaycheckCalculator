using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.NorthDakota;

/// <summary>
/// State module for North Dakota (ND) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// North Dakota Office of State Tax Commissioner employer withholding guide.
/// Employees use the federal Form W-4 for North Dakota withholding purposes
/// (ND adopted the federal W-4 effective 2020; no separate ND withholding
/// certificate exists for new hires).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction (mirrors the federal
///      standard deduction for the applicable year).
///   4. Low-income exemption: floor annual taxable income at zero.
///   5. Apply North Dakota's 2026 graduated income tax brackets.
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested on
///      Form W-4 Step 4(c).
///
/// Filing statuses (per federal Form W-4 and ND withholding guide):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// 2026 North Dakota amounts (ND Office of State Tax Commissioner,
/// 2026 Employer's Withholding Guide):
///   Standard deduction (mirrors federal):
///     Single / MFS:          $15,750
///     Married / QSS:         $31,500
///     Head of Household:     $23,625
///   Tax brackets — Single / MFS:
///     1.10% on $0 – $46,500
///     2.04% on $46,500 – $113,750
///     2.64% over $113,750
///   Tax brackets — Married / QSS:
///     1.10% on $0 – $78,650
///     2.04% on $78,650 – $197,550
///     2.64% over $197,550
///   Tax brackets — Head of Household:
///     1.10% on $0 – $62,100
///     2.04% on $62,100 – $152,100
///     2.64% over $152,100
///
/// Sources:
///   • North Dakota Office of State Tax Commissioner, 2026 Employer's
///     Withholding Guide / Income Tax Withholding Tables.
///   • Federal Form W-4 (used for ND withholding purposes).
/// </summary>
public sealed class NorthDakotaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions (mirror federal) ─────────────────────────

    /// <summary>2026 ND standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 15_750m;

    /// <summary>2026 ND standard deduction for Married Filing Jointly / Qualifying Surviving Spouse.</summary>
    public const decimal StandardDeductionMarried = 31_500m;

    /// <summary>2026 ND standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 23_625m;

    // ── Bracket ceiling thresholds — Single / MFS ────────────────────

    /// <summary>Upper ceiling of the first Single bracket ($46,500).</summary>
    public const decimal SingleBracket1Ceiling = 46_500m;

    /// <summary>Upper ceiling of the second Single bracket ($113,750). Income over this is taxed at 2.64%.</summary>
    public const decimal SingleBracket2Ceiling = 113_750m;

    // ── Bracket ceiling thresholds — Married / QSS ──────────────────

    /// <summary>Upper ceiling of the first Married bracket ($78,650).</summary>
    public const decimal MarriedBracket1Ceiling = 78_650m;

    /// <summary>Upper ceiling of the second Married bracket ($197,550). Income over this is taxed at 2.64%.</summary>
    public const decimal MarriedBracket2Ceiling = 197_550m;

    // ── Bracket ceiling thresholds — Head of Household ──────────────

    /// <summary>Upper ceiling of the first Head of Household bracket ($62,100).</summary>
    public const decimal HohBracket1Ceiling = 62_100m;

    /// <summary>Upper ceiling of the second Head of Household bracket ($152,100). Income over this is taxed at 2.64%.</summary>
    public const decimal HohBracket2Ceiling = 152_100m;

    // ── Tax rates (shared across all filing statuses) ────────────────

    /// <summary>North Dakota rate for the first bracket (1.10%).</summary>
    public const decimal Rate1 = 0.011m;

    /// <summary>North Dakota rate for the second bracket (2.04%).</summary>
    public const decimal Rate2 = 0.0204m;

    /// <summary>North Dakota top rate for the third bracket (2.64%).</summary>
    public const decimal Rate3 = 0.0264m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public NorthDakotaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.ND, "FilingStatus");
    }

    public UsState State => UsState.ND;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the filing-status standard deduction (mirrors federal).
        var standardDeduction = filingStatus switch
        {
            StatusMarried         => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _                     => StandardDeductionSingle
        };

        // Step 4: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages - standardDeduction);

        // Step 5: Apply North Dakota's 2026 graduated brackets (thresholds differ by filing status).
        var annualTax = filingStatus switch
        {
            StatusMarried         => ApplyMarriedBrackets(annualTaxableIncome),
            StatusHeadOfHousehold => ApplyHohBrackets(annualTaxableIncome),
            _                     => ApplySingleBrackets(annualTaxableIncome)
        };

        // Step 6: De-annualize and round to two decimal places.
        var periodTax   = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding.
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
        //   1.10% on $0 – $46,500
        //   2.04% on $46,500 – $113,750
        //   2.64% over $113,750
        if (income <= 0m) return 0m;
        return ComputeTax(income, SingleBracket1Ceiling, SingleBracket2Ceiling);
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // Married / QSS brackets:
        //   1.10% on $0 – $78,650
        //   2.04% on $78,650 – $197,550
        //   2.64% over $197,550
        if (income <= 0m) return 0m;
        return ComputeTax(income, MarriedBracket1Ceiling, MarriedBracket2Ceiling);
    }

    private static decimal ApplyHohBrackets(decimal income)
    {
        // Head of Household brackets:
        //   1.10% on $0 – $62,100
        //   2.04% on $62,100 – $152,100
        //   2.64% over $152,100
        if (income <= 0m) return 0m;
        return ComputeTax(income, HohBracket1Ceiling, HohBracket2Ceiling);
    }

    /// <summary>
    /// Applies North Dakota's three-rate bracket structure using the provided ceiling thresholds.
    /// All three filing statuses share the same three rates; only the bracket thresholds differ.
    /// </summary>
    private static decimal ComputeTax(decimal income, decimal ceiling1, decimal ceiling2)
    {
        decimal tax = 0m;

        // 1.10% on $0 – ceiling1
        tax += Math.Min(income, ceiling1) * Rate1;

        // 2.04% on ceiling1 – ceiling2
        if (income > ceiling1)
            tax += (Math.Min(income, ceiling2) - ceiling1) * Rate2;

        // 2.64% over ceiling2
        if (income > ceiling2)
            tax += (income - ceiling2) * Rate3;

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
