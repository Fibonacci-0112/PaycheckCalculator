using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.SouthCarolina;

/// <summary>
/// State module for South Carolina (SC) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// South Carolina Department of Revenue Form WH-1603F and
/// Form SC W-4 (Employee's Withholding Allowance Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. If the employee claims at least one allowance, subtract the standard
///      deduction: 10% of annualized wages, not to exceed $7,500.
///   4. Subtract the SC W-4 personal allowance deduction ($5,000 per allowance).
///   5. Floor annual taxable income at zero.
///   6. Apply South Carolina's 2026 graduated brackets (0% / 3% / 6%).
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form SC W-4.
///
/// Filing statuses (per Form SC W-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
///   All three statuses use the same graduated bracket schedule.
///   The standard deduction and allowance amounts do not vary by filing status.
///
/// 2026 South Carolina amounts (SCDOR Form WH-1603F):
///   Standard deduction (when allowances ≥ 1):
///     10% of annualized wages, maximum $7,500 (same for all filing statuses)
///   Per-allowance deduction (SC W-4 Line 2): $5,000
///   Brackets (applied to annualized taxable income):
///     $0          – $3,640    →  0%
///     $3,640      – $18,230   →  3%
///     Over $18,230            →  6%
///
/// Sources:
///   • South Carolina Department of Revenue, Form WH-1603F, Employer's
///     Withholding Tax Formula, 2026.
///   • South Carolina Form SC W-4, Employee's Withholding Certificate.
///   • SC Code Ann. § 12-6-510 and SC Act R. 117-40.
/// </summary>
public sealed class SouthCarolinaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deduction ───────────────────────────────────────────

    /// <summary>
    /// Maximum annual standard deduction (applies when at least one allowance
    /// is claimed). South Carolina limits the standard deduction to 10% of
    /// annualized wages not to exceed $7,500 for all filing statuses.
    /// </summary>
    public const decimal StandardDeductionMaximum = 7_500m;

    /// <summary>Standard deduction rate applied to annualized wages (10%).</summary>
    public const decimal StandardDeductionRate = 0.10m;

    // ── Per-allowance deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per allowance claimed on Form SC W-4.
    /// South Carolina uses $5,000 per allowance for tax year 2026.
    /// </summary>
    public const decimal AllowanceAmount = 5_000m;

    // ── Tax brackets ─────────────────────────────────────────────────

    /// <summary>Lower boundary of the 3% bracket.</summary>
    public const decimal Bracket3PctFloor = 3_640m;

    /// <summary>Lower boundary of the 6% bracket.</summary>
    public const decimal Bracket6PctFloor = 18_230m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public SouthCarolinaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.SC, "FilingStatus");
    }

public UsState State => UsState.SC;


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
        var allowances       = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Standard deduction — 10% of annualized wages, max $7,500,
        //         only when at least one allowance is claimed (SCDOR WH-1603F).
        var standardDeduction = allowances > 0
            ? Math.Min(annualWages * StandardDeductionRate, StandardDeductionMaximum)
            : 0m;

        // Step 4: Subtract SC W-4 personal allowance deduction ($5,000 per allowance).
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply South Carolina's 2026 graduated brackets.
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

    // ── Tax computation ───────────────────────────────────────────────

    /// <summary>
    /// Applies South Carolina's 2026 graduated income tax brackets.
    ///   $0        – $3,640:    0%
    ///   $3,640    – $18,230:   3%
    ///   Over $18,230:          6%
    /// </summary>
    private static decimal ApplyBrackets(decimal annualIncome)
    {
        if (annualIncome <= Bracket3PctFloor)
            return 0m;

        if (annualIncome <= Bracket6PctFloor)
            return (annualIncome - Bracket3PctFloor) * 0.03m;

        // Income exceeds the 6% threshold.
        // Tax on 3% band: ($18,230 − $3,640) × 3% = $14,590 × 3% = $437.70
        const decimal taxThrough6PctFloor = (Bracket6PctFloor - Bracket3PctFloor) * 0.03m;
        return taxThrough6PctFloor + (annualIncome - Bracket6PctFloor) * 0.06m;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily        => 260,
        PayFrequency.Weekly       => 52,
        PayFrequency.Biweekly     => 26,
        PayFrequency.Semimonthly  => 24,
        PayFrequency.Monthly      => 12,
        PayFrequency.Quarterly    => 4,
        PayFrequency.Semiannual   => 2,
        PayFrequency.Annual       => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
