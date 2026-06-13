using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Missouri;

/// <summary>
/// State module for Missouri income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Missouri Department of Revenue Employer's Withholding Tax Guide and
/// Form MO W-4 (Employee's Withholding Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract the MO W-4 allowance deduction ($2,100 × number of allowances).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply Missouri's 2026 graduated income tax brackets to annual taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form MO W-4.
///
/// Filing statuses (per Form MO W-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// 2026 Missouri amounts (Missouri DOR Employer's Withholding Tax Guide):
///   Standard deduction: $15,750 (Single) / $31,500 (Married) / $23,625 (Head of Household)
///     Missouri mirrors the 2026 federal standard deduction amounts.
///   Per MO W-4 allowance deduction: $2,100
///   Tax brackets (all filing statuses):
///     0%   on $0 – $1,313
///     2%   on $1,313 – $2,626
///     2.5% on $2,626 – $3,939
///     3%   on $3,939 – $5,252
///     3.5% on $5,252 – $6,565
///     4%   on $6,565 – $7,878
///     4.5% on $7,878 – $9,191
///     4.7% over $9,191
///
/// Sources:
///   • Missouri Department of Revenue, Employer's Withholding Tax Guide, 2026.
///   • Form MO W-4, Employee's Withholding Certificate.
///   • Missouri SB 3 (2022) and SB 5 (2023 Special Session): phase-down of
///     top rate to 4.7% and collapse of lower brackets.
/// </summary>
public sealed class MissouriWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 Missouri standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 15_750m;

    /// <summary>2026 Missouri standard deduction for Married Filing Jointly / Qualifying Surviving Spouse.</summary>
    public const decimal StandardDeductionMarried = 31_500m;

    /// <summary>2026 Missouri standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 23_625m;

    // ── Per-allowance deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per allowance claimed on Form MO W-4.
    /// Missouri uses $2,100 per allowance for tax year 2026.
    /// </summary>
    public const decimal AllowanceAmount = 2_100m;

    // ── Bracket ceiling thresholds ───────────────────────────────────

    /// <summary>Upper bound of the zero-rate bracket.</summary>
    public const decimal Bracket1Ceiling = 1_313m;

    /// <summary>Upper bound of the 2% bracket.</summary>
    public const decimal Bracket2Ceiling = 2_626m;

    /// <summary>Upper bound of the 2.5% bracket.</summary>
    public const decimal Bracket3Ceiling = 3_939m;

    /// <summary>Upper bound of the 3% bracket.</summary>
    public const decimal Bracket4Ceiling = 5_252m;

    /// <summary>Upper bound of the 3.5% bracket.</summary>
    public const decimal Bracket5Ceiling = 6_565m;

    /// <summary>Upper bound of the 4% bracket.</summary>
    public const decimal Bracket6Ceiling = 7_878m;

    /// <summary>Upper bound of the 4.5% bracket. Income over this ceiling is taxed at 4.7%.</summary>
    public const decimal Bracket7Ceiling = 9_191m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>Missouri rate for the first bracket (0% on $0–$1,313).</summary>
    public const decimal Rate1 = 0.00m;

    /// <summary>Missouri rate for the second bracket (2% on $1,313–$2,626).</summary>
    public const decimal Rate2 = 0.02m;

    /// <summary>Missouri rate for the third bracket (2.5% on $2,626–$3,939).</summary>
    public const decimal Rate3 = 0.025m;

    /// <summary>Missouri rate for the fourth bracket (3% on $3,939–$5,252).</summary>
    public const decimal Rate4 = 0.03m;

    /// <summary>Missouri rate for the fifth bracket (3.5% on $5,252–$6,565).</summary>
    public const decimal Rate5 = 0.035m;

    /// <summary>Missouri rate for the sixth bracket (4% on $6,565–$7,878).</summary>
    public const decimal Rate6 = 0.04m;

    /// <summary>Missouri rate for the seventh bracket (4.5% on $7,878–$9,191).</summary>
    public const decimal Rate7 = 0.045m;

    /// <summary>Missouri top rate for income over $9,191 (4.7%).</summary>
    public const decimal Rate8 = 0.047m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MissouriWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MO, "FilingStatus");
    }

    public UsState State => UsState.MO;

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

        // Step 3: Subtract the filing-status standard deduction.
        var standardDeduction = filingStatus switch
        {
            StatusMarried => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _ => StandardDeductionSingle
        };

        // Step 4: Subtract MO W-4 allowance deduction ($2,100 per allowance).
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply Missouri's 2026 graduated brackets.
        //   0%   on $0 – $1,313
        //   2%   on $1,313 – $2,626
        //   2.5% on $2,626 – $3,939
        //   3%   on $3,939 – $5,252
        //   3.5% on $5,252 – $6,565
        //   4%   on $6,565 – $7,878
        //   4.5% on $7,878 – $9,191
        //   4.7% over $9,191
        var annualTax = ApplyBrackets(annualTaxableIncome);

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

    // ── Bracket helper ────────────────────────────────────────────────

    private static decimal ApplyBrackets(decimal income)
    {
        // Missouri uses the same eight brackets for all filing statuses.
        // Only the standard deduction differs by filing status.
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        // 0% on $0 – $1,313 (contributes $0 but listed for completeness)

        // 2% on $1,313 – $2,626
        if (income > Bracket1Ceiling)
        {
            var tier = Math.Min(income, Bracket2Ceiling) - Bracket1Ceiling;
            tax += tier * Rate2;
        }

        // 2.5% on $2,626 – $3,939
        if (income > Bracket2Ceiling)
        {
            var tier = Math.Min(income, Bracket3Ceiling) - Bracket2Ceiling;
            tax += tier * Rate3;
        }

        // 3% on $3,939 – $5,252
        if (income > Bracket3Ceiling)
        {
            var tier = Math.Min(income, Bracket4Ceiling) - Bracket3Ceiling;
            tax += tier * Rate4;
        }

        // 3.5% on $5,252 – $6,565
        if (income > Bracket4Ceiling)
        {
            var tier = Math.Min(income, Bracket5Ceiling) - Bracket4Ceiling;
            tax += tier * Rate5;
        }

        // 4% on $6,565 – $7,878
        if (income > Bracket5Ceiling)
        {
            var tier = Math.Min(income, Bracket6Ceiling) - Bracket5Ceiling;
            tax += tier * Rate6;
        }

        // 4.5% on $7,878 – $9,191
        if (income > Bracket6Ceiling)
        {
            var tier = Math.Min(income, Bracket7Ceiling) - Bracket6Ceiling;
            tax += tier * Rate7;
        }

        // 4.7% over $9,191
        if (income > Bracket7Ceiling)
        {
            var tier = income - Bracket7Ceiling;
            tax += tier * Rate8;
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
