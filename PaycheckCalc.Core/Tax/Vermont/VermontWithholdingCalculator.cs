using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Vermont;

/// <summary>
/// State module for Vermont income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Vermont Department of Taxes publication <em>Income Tax Withholding
/// Instructions, Tables, and Charts</em> (BP-55, 2026).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract Vermont W-4VT allowance amounts ($5,400 per allowance).
///      Vermont withholding uses no standard deduction; the allowance
///      system replaces it entirely.
///   4. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   5. Apply Vermont's graduated income tax brackets to annual taxable income.
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested on
///      Form W-4VT.
///
/// Filing statuses (per Form W-4VT):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Widow(er).
///   • Head of Household — Head of Household (own bracket schedule).
///
/// 2026 Vermont amounts (Vermont Department of Taxes, BP-55, 2026):
///   Per-allowance deduction: $5,400 (no state standard deduction applied
///   to withholding; allowances serve as the only annualized offset)
///   Single brackets:
///     3.35% on $0 – $47,900
///     6.60% on $47,900 – $116,000
///     7.60% on $116,000 – $242,000
///     8.75% over $242,000
///   Married brackets:
///     3.35% on $0 – $79,950
///     6.60% on $79,950 – $193,300
///     7.60% on $193,300 – $294,600
///     8.75% over $294,600
///   Head of Household brackets:
///     3.35% on $0 – $64,200
///     6.60% on $64,200 – $165,700
///     7.60% on $165,700 – $268,300
///     8.75% over $268,300
///
/// Sources:
///   • Vermont Department of Taxes, <em>Income Tax Withholding Instructions,
///     Tables, and Charts</em> (BP-55), effective January 1, 2026.
/// </summary>
public sealed class VermontWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>
    /// Annual deduction per W-4VT allowance (2026).
    /// Vermont withholding uses no state standard deduction; allowances
    /// are the sole annualized offset from gross wages before applying brackets.
    /// </summary>
    public const decimal AllowanceAmount = 5_400m;

    // ── Single bracket thresholds ────────────────────────────────────

    /// <summary>Upper bound of the first (3.35%) single bracket.</summary>
    public const decimal SingleBracket1Ceiling = 47_900m;

    /// <summary>Upper bound of the second (6.60%) single bracket.</summary>
    public const decimal SingleBracket2Ceiling = 116_000m;

    /// <summary>Upper bound of the third (7.60%) single bracket.</summary>
    public const decimal SingleBracket3Ceiling = 242_000m;

    // ── Married bracket thresholds ───────────────────────────────────

    /// <summary>Upper bound of the first (3.35%) married bracket.</summary>
    public const decimal MarriedBracket1Ceiling = 79_950m;

    /// <summary>Upper bound of the second (6.60%) married bracket.</summary>
    public const decimal MarriedBracket2Ceiling = 193_300m;

    /// <summary>Upper bound of the third (7.60%) married bracket.</summary>
    public const decimal MarriedBracket3Ceiling = 294_600m;

    // ── Head of Household bracket thresholds ─────────────────────────

    /// <summary>Upper bound of the first (3.35%) head of household bracket.</summary>
    public const decimal HohBracket1Ceiling = 64_200m;

    /// <summary>Upper bound of the second (6.60%) head of household bracket.</summary>
    public const decimal HohBracket2Ceiling = 165_700m;

    /// <summary>Upper bound of the third (7.60%) head of household bracket.</summary>
    public const decimal HohBracket3Ceiling = 268_300m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>Vermont income tax rate for the first bracket (3.35%).</summary>
    public const decimal Rate1 = 0.0335m;

    /// <summary>Vermont income tax rate for the second bracket (6.60%).</summary>
    public const decimal Rate2 = 0.066m;

    /// <summary>Vermont income tax rate for the third bracket (7.60%).</summary>
    public const decimal Rate3 = 0.076m;

    /// <summary>Vermont income tax rate for the top bracket (8.75%).</summary>
    public const decimal Rate4 = 0.0875m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public VermontWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.VT, "FilingStatus");
    }

public UsState State => UsState.VT;


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

        // Step 3: Subtract allowances ($5,400 per allowance).
        // Vermont has no state standard deduction for withholding purposes;
        // the allowance system is the only annualized offset.
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 4: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages - allowanceDeduction);

        // Step 5: Apply graduated brackets based on W-4VT filing status.
        var annualTax = filingStatus switch
        {
            StatusMarried => ApplyMarriedBrackets(annualTaxableIncome),
            StatusHeadOfHousehold => ApplyHeadOfHouseholdBrackets(annualTaxableIncome),
            _ => ApplySingleBrackets(annualTaxableIncome)
        };

        // Step 6: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding.
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
        // 3.35% on $0 – $47,900
        // 6.60% on $47,900 – $116,000
        // 7.60% on $116,000 – $242,000
        // 8.75% over $242,000
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
        // 3.35% on $0 – $79,950
        // 6.60% on $79,950 – $193,300
        // 7.60% on $193,300 – $294,600
        // 8.75% over $294,600
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
        // 3.35% on $0 – $64,200
        // 6.60% on $64,200 – $165,700
        // 7.60% on $165,700 – $268,300
        // 8.75% over $268,300
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
