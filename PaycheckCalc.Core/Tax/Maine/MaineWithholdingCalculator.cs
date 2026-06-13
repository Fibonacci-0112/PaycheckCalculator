using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Maine;

/// <summary>
/// State module for Maine income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Maine Revenue Services publication <em>Maine Income Tax Withholding</em>
/// (2026 Withholding Tables).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract Maine W-4ME allowance amounts ($5,300 per allowance).
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Maine's graduated income tax brackets to annual taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form W-4ME.
///
/// Filing statuses (per Form W-4ME):
///   • Single  — also used for Head of Household and Married Filing Separately.
///   • Married — Married Filing Jointly.
///
/// 2026 Maine amounts (Maine Revenue Services, 2026 Withholding Tables):
///   Standard deduction: $15,300 (Single) / $30,600 (Married)
///   Per-allowance deduction: $5,300
///   Single brackets:
///     5.80% on $0 – $27,400
///     6.75% on $27,401 – $64,850
///     7.15% over $64,850
///   Married brackets:
///     5.80% on $0 – $54,850
///     6.75% on $54,851 – $129,750
///     7.15% over $129,750
///
/// Sources:
///   • Maine Revenue Services, <em>Maine Income Tax Withholding</em>,
///     2026 Withholding Tables.
/// </summary>
public sealed class MaineWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>2026 Maine standard deduction for Single / HoH / MFS.</summary>
    public const decimal StandardDeductionSingle = 15_300m;

    /// <summary>2026 Maine standard deduction for Married Filing Jointly.</summary>
    public const decimal StandardDeductionMarried = 30_600m;

    /// <summary>
    /// Annual deduction per W-4ME allowance.
    /// Subtracted from annual taxable wages after the standard deduction.
    /// </summary>
    public const decimal AllowanceAmount = 5_300m;

    // ── Single bracket thresholds ────────────────────────────────────

    /// <summary>Upper bound of the first (5.80%) single bracket.</summary>
    public const decimal SingleBracket1Ceiling = 27_400m;

    /// <summary>Upper bound of the second (6.75%) single bracket.</summary>
    public const decimal SingleBracket2Ceiling = 64_850m;

    // ── Married bracket thresholds ───────────────────────────────────

    /// <summary>Upper bound of the first (5.80%) married bracket.</summary>
    public const decimal MarriedBracket1Ceiling = 54_850m;

    /// <summary>Upper bound of the second (6.75%) married bracket.</summary>
    public const decimal MarriedBracket2Ceiling = 129_750m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>Maine income tax rate for the first bracket (5.80%).</summary>
    public const decimal Rate1 = 0.058m;

    /// <summary>Maine income tax rate for the second bracket (6.75%).</summary>
    public const decimal Rate2 = 0.0675m;

    /// <summary>Maine income tax rate for the top bracket (7.15%).</summary>
    public const decimal Rate3 = 0.0715m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MaineWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.ME, "FilingStatus");
    }

    public UsState State => UsState.ME;

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
        var allowances = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract standard deduction for the W-4ME filing status.
        var standardDeduction = filingStatus == StatusMarried
            ? StandardDeductionMarried
            : StandardDeductionSingle;

        // Step 4: Subtract allowances.
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply graduated brackets.
        var annualTax = filingStatus == StatusMarried
            ? ApplyMarriedBrackets(annualTaxableIncome)
            : ApplySingleBrackets(annualTaxableIncome);

        // Step 7: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    // ── Bracket helpers ───────────────────────────────────────────────

    private static decimal ApplySingleBrackets(decimal income)
    {
        // 5.80% on $0 – $27,400
        // 6.75% on $27,401 – $64,850
        // 7.15% over $64,850
        decimal tax = 0m;

        if (income <= 0m)
            return 0m;

        var tier1 = Math.Min(income, SingleBracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > SingleBracket1Ceiling)
        {
            var tier2 = Math.Min(income, SingleBracket2Ceiling) - SingleBracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > SingleBracket2Ceiling)
        {
            var tier3 = income - SingleBracket2Ceiling;
            tax += tier3 * Rate3;
        }

        return tax;
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // 5.80% on $0 – $54,850
        // 6.75% on $54,851 – $129,750
        // 7.15% over $129,750
        decimal tax = 0m;

        if (income <= 0m)
            return 0m;

        var tier1 = Math.Min(income, MarriedBracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > MarriedBracket1Ceiling)
        {
            var tier2 = Math.Min(income, MarriedBracket2Ceiling) - MarriedBracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > MarriedBracket2Ceiling)
        {
            var tier3 = income - MarriedBracket2Ceiling;
            tax += tier3 * Rate3;
        }

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

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
