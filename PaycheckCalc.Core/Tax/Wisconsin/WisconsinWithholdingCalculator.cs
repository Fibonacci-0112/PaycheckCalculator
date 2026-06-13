using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Wisconsin;

/// <summary>
/// State module for Wisconsin income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Wisconsin Department of Revenue <em>Employer's Withholding Tax Guide</em>
/// (Publication W-166, 2026) and the accompanying Circular WT.
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract WT-4 allowance deductions (allowances × $700).
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Wisconsin's graduated income tax brackets to annual taxable income.
///      (Head of Household shares Single bracket thresholds.)
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form WT-4.
///
/// Filing statuses (per Form WT-4):
///   • Single           — Single filers (higher withholding rate).
///   • Married          — Married Filing Jointly or Qualifying Widow(er).
///   • Head of Household — Head of Household (own standard deduction;
///                         shares Single bracket thresholds).
///
/// 2026 Wisconsin amounts (WI DOR, Publication W-166 / Circular WT, 2026):
///   Standard deduction: $12,760 (Single) / $23,170 (Married) / $16,840 (Head of Household)
///   Per-WT-4-allowance deduction: $700
///   Single and Head of Household brackets:
///     3.54% on $0         – $13,810
///     4.65% on $13,810    – $27,630
///     5.30% on $27,630    – $304,170
///     7.65% over $304,170
///   Married brackets:
///     3.54% on $0         – $18,410
///     4.65% on $18,410    – $36,820
///     5.30% on $36,820    – $405,550
///     7.65% over $405,550
///
/// Sources:
///   • Wisconsin Department of Revenue, <em>Employer's Withholding Tax Guide</em>
///     (Publication W-166), effective 2026.
///   • Wisconsin Department of Revenue, Circular WT (2026 withholding tables
///     and percentage-method formula).
/// </summary>
public sealed class WisconsinWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ───────────────────────────────────────────

    /// <summary>2026 Wisconsin annualized standard deduction for Single filers.</summary>
    public const decimal StandardDeductionSingle = 12_760m;

    /// <summary>2026 Wisconsin annualized standard deduction for Married filers.</summary>
    public const decimal StandardDeductionMarried = 23_170m;

    /// <summary>2026 Wisconsin annualized standard deduction for Head of Household filers.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 16_840m;

    // ── Allowance ────────────────────────────────────────────────────

    /// <summary>Annual deduction per WT-4 allowance (exemption) claimed (2026).</summary>
    public const decimal AllowanceAmount = 700m;

    // ── Single / Head of Household bracket thresholds ────────────────

    /// <summary>Upper bound of the first (3.54%) single/HoH bracket.</summary>
    public const decimal SingleBracket1Ceiling = 13_810m;

    /// <summary>Upper bound of the second (4.65%) single/HoH bracket.</summary>
    public const decimal SingleBracket2Ceiling = 27_630m;

    /// <summary>Upper bound of the third (5.30%) single/HoH bracket.</summary>
    public const decimal SingleBracket3Ceiling = 304_170m;

    // ── Married bracket thresholds ────────────────────────────────────

    /// <summary>Upper bound of the first (3.54%) married bracket.</summary>
    public const decimal MarriedBracket1Ceiling = 18_410m;

    /// <summary>Upper bound of the second (4.65%) married bracket.</summary>
    public const decimal MarriedBracket2Ceiling = 36_820m;

    /// <summary>Upper bound of the third (5.30%) married bracket.</summary>
    public const decimal MarriedBracket3Ceiling = 405_550m;

    // ── Tax rates ─────────────────────────────────────────────────────

    /// <summary>Wisconsin income tax rate for the first bracket (3.54%).</summary>
    public const decimal Rate1 = 0.0354m;

    /// <summary>Wisconsin income tax rate for the second bracket (4.65%).</summary>
    public const decimal Rate2 = 0.0465m;

    /// <summary>Wisconsin income tax rate for the third bracket (5.30%).</summary>
    public const decimal Rate3 = 0.053m;

    /// <summary>Wisconsin income tax rate for the top bracket (7.65%).</summary>
    public const decimal Rate4 = 0.0765m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public WisconsinWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.WI, "FilingStatus");
    }

public UsState State => UsState.WI;


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

        // Step 3: Subtract standard deduction for the WT-4 filing status.
        var standardDeduction = filingStatus switch
        {
            StatusMarried => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _ => StandardDeductionSingle
        };

        // Step 4: Subtract allowance deductions ($700 per WT-4 allowance).
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply graduated brackets.
        // Head of Household shares Single bracket thresholds.
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
        // Single and Head of Household share these thresholds:
        //   3.54% on $0         – $13,810
        //   4.65% on $13,810    – $27,630
        //   5.30% on $27,630    – $304,170
        //   7.65% over $304,170
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        var tier1 = Math.Min(income, SingleBracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > SingleBracket1Ceiling)
        {
            var tier2 = Math.Min(income, SingleBracket2Ceiling) - SingleBracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > SingleBracket2Ceiling)
        {
            var tier3 = Math.Min(income, SingleBracket3Ceiling) - SingleBracket2Ceiling;
            tax += tier3 * Rate3;
        }

        if (income > SingleBracket3Ceiling)
        {
            var tier4 = income - SingleBracket3Ceiling;
            tax += tier4 * Rate4;
        }

        return tax;
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        //   3.54% on $0         – $18,410
        //   4.65% on $18,410    – $36,820
        //   5.30% on $36,820    – $405,550
        //   7.65% over $405,550
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        var tier1 = Math.Min(income, MarriedBracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > MarriedBracket1Ceiling)
        {
            var tier2 = Math.Min(income, MarriedBracket2Ceiling) - MarriedBracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > MarriedBracket2Ceiling)
        {
            var tier3 = Math.Min(income, MarriedBracket3Ceiling) - MarriedBracket2Ceiling;
            tax += tier3 * Rate3;
        }

        if (income > MarriedBracket3Ceiling)
        {
            var tier4 = income - MarriedBracket3Ceiling;
            tax += tier4 * Rate4;
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
