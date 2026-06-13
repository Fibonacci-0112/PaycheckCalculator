using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Nebraska;

/// <summary>
/// State module for Nebraska (NE) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Nebraska Department of Revenue Circular EN and Form W-4N
/// (Nebraska Employee's Withholding Allowance Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Low-income exemption: floor annual taxable income at zero.
///   5. Apply Nebraska's 2026 graduated income tax brackets.
///   6. Subtract the W-4N allowance credit ($171 per allowance claimed).
///   7. Floor computed annual tax at zero (credits cannot create a refund).
///   8. De-annualize (÷ pay periods per year) and round to two decimal places.
///   9. Add any additional per-period withholding the employee requested on
///      Form W-4N.
///
/// Filing statuses (per Form W-4N):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// 2026 Nebraska amounts (Nebraska Department of Revenue Circular EN):
///   Standard deduction:
///     Single / MFS:         $8,600
///     Married / QSS:        $17,200
///     Head of Household:    $12,900
///   Per-allowance credit (W-4N): $171 (applied to computed annual tax)
///   Tax brackets:
///     Single / MFS:
///       2.46% on $0 – $4,030
///       3.51% on $4,030 – $24,120
///       5.01% on $24,120 – $38,870
///       5.2%  over $38,870
///     Married / QSS:
///       2.46% on $0 – $8,040
///       3.51% on $8,040 – $48,250
///       5.01% on $48,250 – $77,730
///       5.2%  over $77,730
///     Head of Household:
///       2.46% on $0 – $6,060
///       3.51% on $6,060 – $36,180
///       5.01% on $36,180 – $58,310
///       5.2%  over $58,310
///
/// Sources:
///   • Nebraska Department of Revenue, Circular EN, 2026.
///   • Form W-4N, Nebraska Employee's Withholding Allowance Certificate.
///   • Nebraska LB 754 (2023): phased income tax rate reductions.
/// </summary>
public sealed class NebraskaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 Nebraska standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 8_600m;

    /// <summary>2026 Nebraska standard deduction for Married Filing Jointly / Qualifying Surviving Spouse.</summary>
    public const decimal StandardDeductionMarried = 17_200m;

    /// <summary>2026 Nebraska standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 12_900m;

    // ── Per-allowance credit ─────────────────────────────────────────

    /// <summary>
    /// Annual credit per allowance claimed on Form W-4N.
    /// Nebraska uses $171 per allowance for tax year 2026.
    /// This credit reduces computed annual tax (not taxable income).
    /// </summary>
    public const decimal AllowanceCreditAmount = 171m;

    // ── Bracket ceiling thresholds — Single / MFS ────────────────────

    /// <summary>Upper ceiling of the first Single bracket ($4,030).</summary>
    public const decimal SingleBracket1Ceiling = 4_030m;

    /// <summary>Upper ceiling of the second Single bracket ($24,120).</summary>
    public const decimal SingleBracket2Ceiling = 24_120m;

    /// <summary>Upper ceiling of the third Single bracket ($38,870). Income over this is taxed at 5.2%.</summary>
    public const decimal SingleBracket3Ceiling = 38_870m;

    // ── Bracket ceiling thresholds — Married / QSS ──────────────────

    /// <summary>Upper ceiling of the first Married bracket ($8,040).</summary>
    public const decimal MarriedBracket1Ceiling = 8_040m;

    /// <summary>Upper ceiling of the second Married bracket ($48,250).</summary>
    public const decimal MarriedBracket2Ceiling = 48_250m;

    /// <summary>Upper ceiling of the third Married bracket ($77,730). Income over this is taxed at 5.2%.</summary>
    public const decimal MarriedBracket3Ceiling = 77_730m;

    // ── Bracket ceiling thresholds — Head of Household ──────────────

    /// <summary>Upper ceiling of the first Head of Household bracket ($6,060).</summary>
    public const decimal HohBracket1Ceiling = 6_060m;

    /// <summary>Upper ceiling of the second Head of Household bracket ($36,180).</summary>
    public const decimal HohBracket2Ceiling = 36_180m;

    /// <summary>Upper ceiling of the third Head of Household bracket ($58,310). Income over this is taxed at 5.2%.</summary>
    public const decimal HohBracket3Ceiling = 58_310m;

    // ── Tax rates (shared across all filing statuses) ────────────────

    /// <summary>Nebraska rate for the first bracket (2.46%).</summary>
    public const decimal Rate1 = 0.0246m;

    /// <summary>Nebraska rate for the second bracket (3.51%).</summary>
    public const decimal Rate2 = 0.0351m;

    /// <summary>Nebraska rate for the third bracket (5.01%).</summary>
    public const decimal Rate3 = 0.0501m;

    /// <summary>Nebraska top rate for the fourth bracket (5.2%).</summary>
    public const decimal Rate4 = 0.052m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public NebraskaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.NE, "FilingStatus");
    }

    public UsState State => UsState.NE;

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
        var standardDeduction = filingStatus switch
        {
            StatusMarried         => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _                     => StandardDeductionSingle
        };

        // Step 4: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages - standardDeduction);

        // Step 5: Apply Nebraska's 2026 graduated brackets (thresholds differ by filing status).
        var annualTax = filingStatus switch
        {
            StatusMarried         => ApplyMarriedBrackets(annualTaxableIncome),
            StatusHeadOfHousehold => ApplyHohBrackets(annualTaxableIncome),
            _                     => ApplySingleBrackets(annualTaxableIncome)
        };

        // Step 6: Subtract the W-4N allowance credit ($171 per allowance).
        annualTax -= allowances * AllowanceCreditAmount;

        // Step 7: Floor at zero — credits cannot create a refund.
        annualTax = Math.Max(0m, annualTax);

        // Step 8: De-annualize and round to two decimal places.
        var periodTax   = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 9: Add any per-period extra withholding.
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
        //   2.46% on $0 – $4,030
        //   3.51% on $4,030 – $24,120
        //   5.01% on $24,120 – $38,870
        //   5.2%  over $38,870
        if (income <= 0m) return 0m;
        return ComputeTax(income, SingleBracket1Ceiling, SingleBracket2Ceiling, SingleBracket3Ceiling);
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // Married / QSS brackets:
        //   2.46% on $0 – $8,040
        //   3.51% on $8,040 – $48,250
        //   5.01% on $48,250 – $77,730
        //   5.2%  over $77,730
        if (income <= 0m) return 0m;
        return ComputeTax(income, MarriedBracket1Ceiling, MarriedBracket2Ceiling, MarriedBracket3Ceiling);
    }

    private static decimal ApplyHohBrackets(decimal income)
    {
        // Head of Household brackets:
        //   2.46% on $0 – $6,060
        //   3.51% on $6,060 – $36,180
        //   5.01% on $36,180 – $58,310
        //   5.2%  over $58,310
        if (income <= 0m) return 0m;
        return ComputeTax(income, HohBracket1Ceiling, HohBracket2Ceiling, HohBracket3Ceiling);
    }

    /// <summary>
    /// Applies Nebraska's four-rate bracket structure using the provided ceiling thresholds.
    /// All three filing statuses share the same four rates; only the bracket thresholds differ.
    /// </summary>
    private static decimal ComputeTax(
        decimal income,
        decimal ceiling1,
        decimal ceiling2,
        decimal ceiling3)
    {
        decimal tax = 0m;

        // 2.46% on $0 – ceiling1
        tax += Math.Min(income, ceiling1) * Rate1;

        // 3.51% on ceiling1 – ceiling2
        if (income > ceiling1)
            tax += (Math.Min(income, ceiling2) - ceiling1) * Rate2;

        // 5.01% on ceiling2 – ceiling3
        if (income > ceiling2)
            tax += (Math.Min(income, ceiling3) - ceiling2) * Rate3;

        // 5.2% over ceiling3
        if (income > ceiling3)
            tax += (income - ceiling3) * Rate4;

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
