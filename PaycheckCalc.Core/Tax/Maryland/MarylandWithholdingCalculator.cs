using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Maryland;

/// <summary>
/// State module for Maryland (MD) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Maryland Employer Withholding Guide (Comptroller of Maryland, 2026).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Compute the variable standard deduction: 15% of annual wages,
///      bounded by the filing-status minimum and maximum.
///   4. Subtract the standard deduction and exemption amounts
///      (MW507 exemptions × $3,200 each).
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Maryland's graduated income tax brackets to annual taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form MW507.
///
/// Filing statuses (per Form MW507):
///   • Single           — single filers and married filing separately.
///   • Married          — married filing jointly.
///   • Head of Household — uses married standard-deduction limits and
///                         the married rate schedule (MD Employer Guide).
///
/// 2026 Maryland amounts (Comptroller of Maryland, 2026 Withholding Guide):
///   Standard deduction: 15% of annual wages
///     Single:           minimum $1,600 / maximum $2,550
///     Married / HoH:    minimum $3,200 / maximum $5,100
///   Per-exemption deduction: $3,200
///   Single rate schedule:
///     2.00% on $0 – $1,000
///     3.00% on $1,001 – $2,000
///     4.00% on $2,001 – $3,000
///     4.75% on $3,001 – $100,000
///     5.00% on $100,001 – $125,000
///     5.25% on $125,001 – $150,000
///     5.50% on $150,001 – $250,000
///     5.75% on $250,001 – $500,000
///     6.25% on $500,001 – $1,000,000
///     6.50% over $1,000,000
///   Married / Head of Household rate schedule:
///     2.00% on $0 – $1,000
///     3.00% on $1,001 – $2,000
///     4.00% on $2,001 – $3,000
///     4.75% on $3,001 – $150,000
///     5.00% on $150,001 – $175,000
///     5.25% on $175,001 – $225,000
///     5.50% on $225,001 – $300,000
///     5.75% on $300,001 – $600,000
///     6.25% on $600,001 – $1,200,000
///     6.50% over $1,200,000
///
/// Sources:
///   • Comptroller of Maryland, <em>Maryland Employer Withholding Guide</em>,
///     2026 edition (bFile, Form MW507).
/// </summary>
public sealed class MarylandWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deduction constants ─────────────────────────────────

    /// <summary>Standard deduction rate applied to annual wages.</summary>
    public const decimal StandardDeductionRate = 0.15m;

    /// <summary>Minimum standard deduction for Single / MFS filers.</summary>
    public const decimal StandardDeductionSingleMin = 1_600m;

    /// <summary>Maximum standard deduction for Single / MFS filers.</summary>
    public const decimal StandardDeductionSingleMax = 2_550m;

    /// <summary>Minimum standard deduction for Married / Head of Household filers.</summary>
    public const decimal StandardDeductionMarriedMin = 3_200m;

    /// <summary>Maximum standard deduction for Married / Head of Household filers.</summary>
    public const decimal StandardDeductionMarriedMax = 5_100m;

    // ── Exemption constant ───────────────────────────────────────────

    /// <summary>Annual deduction per MW507 personal exemption.</summary>
    public const decimal ExemptionAmount = 3_200m;

    // ── Single bracket thresholds ────────────────────────────────────

    public const decimal SingleBracket1Ceiling   =     1_000m;
    public const decimal SingleBracket2Ceiling   =     2_000m;
    public const decimal SingleBracket3Ceiling   =     3_000m;
    public const decimal SingleBracket4Ceiling   =   100_000m;
    public const decimal SingleBracket5Ceiling   =   125_000m;
    public const decimal SingleBracket6Ceiling   =   150_000m;
    public const decimal SingleBracket7Ceiling   =   250_000m;
    public const decimal SingleBracket8Ceiling   =   500_000m;
    public const decimal SingleBracket9Ceiling   = 1_000_000m;

    // ── Married / Head of Household bracket thresholds ───────────────

    public const decimal MarriedBracket1Ceiling  =     1_000m;
    public const decimal MarriedBracket2Ceiling  =     2_000m;
    public const decimal MarriedBracket3Ceiling  =     3_000m;
    public const decimal MarriedBracket4Ceiling  =   150_000m;
    public const decimal MarriedBracket5Ceiling  =   175_000m;
    public const decimal MarriedBracket6Ceiling  =   225_000m;
    public const decimal MarriedBracket7Ceiling  =   300_000m;
    public const decimal MarriedBracket8Ceiling  =   600_000m;
    public const decimal MarriedBracket9Ceiling  = 1_200_000m;

    // ── Tax rates (shared by both schedules) ─────────────────────────

    public const decimal Rate1  = 0.02m;
    public const decimal Rate2  = 0.03m;
    public const decimal Rate3  = 0.04m;
    public const decimal Rate4  = 0.0475m;
    public const decimal Rate5  = 0.05m;
    public const decimal Rate6  = 0.0525m;
    public const decimal Rate7  = 0.055m;
    public const decimal Rate8  = 0.0575m;
    public const decimal Rate9  = 0.0625m;
    public const decimal Rate10 = 0.065m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MarylandWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MD, "FilingStatus");
    }

    public UsState State => UsState.MD;

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
        var filingStatus    = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var exemptions      = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        bool isMarriedOrHoH = filingStatus == StatusMarried
                           || filingStatus == StatusHeadOfHousehold;

        // Step 3: Variable standard deduction — 15% of annual wages, bounded
        //         by the filing-status minimum and maximum per the MD guide.
        var (stdMin, stdMax) = isMarriedOrHoH
            ? (StandardDeductionMarriedMin, StandardDeductionMarriedMax)
            : (StandardDeductionSingleMin,  StandardDeductionSingleMax);

        var standardDeduction = Math.Max(stdMin,
            Math.Min(annualWages * StandardDeductionRate, stdMax));

        // Step 4: Subtract standard deduction and exemption amounts.
        var exemptionDeduction = exemptions * ExemptionAmount;

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - exemptionDeduction);

        // Step 6: Apply graduated brackets.
        var annualTax = isMarriedOrHoH
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
            Withholding  = withholding
        };
    }

    // ── Bracket helpers ───────────────────────────────────────────────

    private static decimal ApplySingleBrackets(decimal income)
    {
        // 2.00% on $0 – $1,000
        // 3.00% on $1,001 – $2,000
        // 4.00% on $2,001 – $3,000
        // 4.75% on $3,001 – $100,000
        // 5.00% on $100,001 – $125,000
        // 5.25% on $125,001 – $150,000
        // 5.50% on $150,001 – $250,000
        // 5.75% on $250,001 – $500,000
        // 6.25% on $500,001 – $1,000,000
        // 6.50% over $1,000,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, SingleBracket1Ceiling) * Rate1;

        if (income > SingleBracket1Ceiling)
            tax += (Math.Min(income, SingleBracket2Ceiling) - SingleBracket1Ceiling) * Rate2;

        if (income > SingleBracket2Ceiling)
            tax += (Math.Min(income, SingleBracket3Ceiling) - SingleBracket2Ceiling) * Rate3;

        if (income > SingleBracket3Ceiling)
            tax += (Math.Min(income, SingleBracket4Ceiling) - SingleBracket3Ceiling) * Rate4;

        if (income > SingleBracket4Ceiling)
            tax += (Math.Min(income, SingleBracket5Ceiling) - SingleBracket4Ceiling) * Rate5;

        if (income > SingleBracket5Ceiling)
            tax += (Math.Min(income, SingleBracket6Ceiling) - SingleBracket5Ceiling) * Rate6;

        if (income > SingleBracket6Ceiling)
            tax += (Math.Min(income, SingleBracket7Ceiling) - SingleBracket6Ceiling) * Rate7;

        if (income > SingleBracket7Ceiling)
            tax += (Math.Min(income, SingleBracket8Ceiling) - SingleBracket7Ceiling) * Rate8;

        if (income > SingleBracket8Ceiling)
            tax += (Math.Min(income, SingleBracket9Ceiling) - SingleBracket8Ceiling) * Rate9;

        if (income > SingleBracket9Ceiling)
            tax += (income - SingleBracket9Ceiling) * Rate10;

        return tax;
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // 2.00% on $0 – $1,000
        // 3.00% on $1,001 – $2,000
        // 4.00% on $2,001 – $3,000
        // 4.75% on $3,001 – $150,000
        // 5.00% on $150,001 – $175,000
        // 5.25% on $175,001 – $225,000
        // 5.50% on $225,001 – $300,000
        // 5.75% on $300,001 – $600,000
        // 6.25% on $600,001 – $1,200,000
        // 6.50% over $1,200,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, MarriedBracket1Ceiling) * Rate1;

        if (income > MarriedBracket1Ceiling)
            tax += (Math.Min(income, MarriedBracket2Ceiling) - MarriedBracket1Ceiling) * Rate2;

        if (income > MarriedBracket2Ceiling)
            tax += (Math.Min(income, MarriedBracket3Ceiling) - MarriedBracket2Ceiling) * Rate3;

        if (income > MarriedBracket3Ceiling)
            tax += (Math.Min(income, MarriedBracket4Ceiling) - MarriedBracket3Ceiling) * Rate4;

        if (income > MarriedBracket4Ceiling)
            tax += (Math.Min(income, MarriedBracket5Ceiling) - MarriedBracket4Ceiling) * Rate5;

        if (income > MarriedBracket5Ceiling)
            tax += (Math.Min(income, MarriedBracket6Ceiling) - MarriedBracket5Ceiling) * Rate6;

        if (income > MarriedBracket6Ceiling)
            tax += (Math.Min(income, MarriedBracket7Ceiling) - MarriedBracket6Ceiling) * Rate7;

        if (income > MarriedBracket7Ceiling)
            tax += (Math.Min(income, MarriedBracket8Ceiling) - MarriedBracket7Ceiling) * Rate8;

        if (income > MarriedBracket8Ceiling)
            tax += (Math.Min(income, MarriedBracket9Ceiling) - MarriedBracket8Ceiling) * Rate9;

        if (income > MarriedBracket9Ceiling)
            tax += (income - MarriedBracket9Ceiling) * Rate10;

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
