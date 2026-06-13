using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Minnesota;

/// <summary>
/// State module for Minnesota income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Minnesota Department of Revenue publication <em>Withholding Tax Instructions
/// and Tables</em> (2026, Pub. 89).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract W-4MN allowance amounts ($5,300 per allowance).
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Minnesota's graduated income tax brackets to annual taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form W-4MN.
///
/// Filing statuses (per Form W-4MN):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Widow(er).
///   • Head of Household — Head of Household (own bracket schedule and
///                         standard deduction; rate schedule matches Married).
///
/// 2026 Minnesota amounts (Minnesota Department of Revenue, Pub. 89):
///   Standard deduction: $15,300 (Single) / $30,600 (Married) / $23,000 (Head of Household)
///   Per-allowance deduction: $5,300
///   Single brackets:
///     5.35% on $0 – $33,310
///     6.80% on $33,310 – $109,430
///     7.85% on $109,430 – $203,150
///     9.85% over $203,150
///   Married brackets:
///     5.35% on $0 – $48,700
///     6.80% on $48,700 – $193,480
///     7.85% on $193,480 – $337,930
///     9.85% over $337,930
///   Head of Household brackets:
///     5.35% on $0 – $41,010
///     6.80% on $41,010 – $164,800
///     7.85% on $164,800 – $270,060
///     9.85% over $270,060
///
/// Sources:
///   • Minnesota Department of Revenue, <em>Withholding Tax Instructions
///     and Tables</em>, 2026 (Pub. 89).
/// </summary>
public sealed class MinnesotaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>2026 Minnesota standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 15_300m;

    /// <summary>2026 Minnesota standard deduction for Married Filing Jointly / Qualifying Widow(er).</summary>
    public const decimal StandardDeductionMarried = 30_600m;

    /// <summary>2026 Minnesota standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 23_000m;

    /// <summary>
    /// Annual deduction per W-4MN allowance.
    /// Subtracted from annual taxable wages after the standard deduction.
    /// </summary>
    public const decimal AllowanceAmount = 5_300m;

    // ── Single bracket thresholds ────────────────────────────────────

    /// <summary>Upper bound of the first (5.35%) single bracket.</summary>
    public const decimal SingleBracket1Ceiling = 33_310m;

    /// <summary>Upper bound of the second (6.80%) single bracket.</summary>
    public const decimal SingleBracket2Ceiling = 109_430m;

    /// <summary>Upper bound of the third (7.85%) single bracket.</summary>
    public const decimal SingleBracket3Ceiling = 203_150m;

    // ── Married bracket thresholds ───────────────────────────────────

    /// <summary>Upper bound of the first (5.35%) married bracket.</summary>
    public const decimal MarriedBracket1Ceiling = 48_700m;

    /// <summary>Upper bound of the second (6.80%) married bracket.</summary>
    public const decimal MarriedBracket2Ceiling = 193_480m;

    /// <summary>Upper bound of the third (7.85%) married bracket.</summary>
    public const decimal MarriedBracket3Ceiling = 337_930m;

    // ── Head of Household bracket thresholds ─────────────────────────

    /// <summary>Upper bound of the first (5.35%) head of household bracket.</summary>
    public const decimal HohBracket1Ceiling = 41_010m;

    /// <summary>Upper bound of the second (6.80%) head of household bracket.</summary>
    public const decimal HohBracket2Ceiling = 164_800m;

    /// <summary>Upper bound of the third (7.85%) head of household bracket.</summary>
    public const decimal HohBracket3Ceiling = 270_060m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>Minnesota income tax rate for the first bracket (5.35%).</summary>
    public const decimal Rate1 = 0.0535m;

    /// <summary>Minnesota income tax rate for the second bracket (6.80%).</summary>
    public const decimal Rate2 = 0.068m;

    /// <summary>Minnesota income tax rate for the third bracket (7.85%).</summary>
    public const decimal Rate3 = 0.0785m;

    /// <summary>Minnesota income tax rate for the top bracket (9.85%).</summary>
    public const decimal Rate4 = 0.0985m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MinnesotaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MN, "FilingStatus");
    }

    public UsState State => UsState.MN;

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

        // Step 3: Subtract standard deduction for the W-4MN filing status.
        var standardDeduction = filingStatus switch
        {
            StatusMarried => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _ => StandardDeductionSingle
        };

        // Step 4: Subtract allowances.
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply graduated brackets.
        var annualTax = filingStatus switch
        {
            StatusMarried => ApplyMarriedBrackets(annualTaxableIncome),
            StatusHeadOfHousehold => ApplyHeadOfHouseholdBrackets(annualTaxableIncome),
            _ => ApplySingleBrackets(annualTaxableIncome)
        };

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
        // 5.35% on $0 – $33,310
        // 6.80% on $33,310 – $109,430
        // 7.85% on $109,430 – $203,150
        // 9.85% over $203,150
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
        // 5.35% on $0 – $48,700
        // 6.80% on $48,700 – $193,480
        // 7.85% on $193,480 – $337,930
        // 9.85% over $337,930
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

    private static decimal ApplyHeadOfHouseholdBrackets(decimal income)
    {
        // 5.35% on $0 – $41,010
        // 6.80% on $41,010 – $164,800
        // 7.85% on $164,800 – $270,060
        // 9.85% over $270,060
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        var tier1 = Math.Min(income, HohBracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > HohBracket1Ceiling)
        {
            var tier2 = Math.Min(income, HohBracket2Ceiling) - HohBracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > HohBracket2Ceiling)
        {
            var tier3 = Math.Min(income, HohBracket3Ceiling) - HohBracket2Ceiling;
            tax += tier3 * Rate3;
        }

        if (income > HohBracket3Ceiling)
        {
            var tier4 = income - HohBracket3Ceiling;
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
