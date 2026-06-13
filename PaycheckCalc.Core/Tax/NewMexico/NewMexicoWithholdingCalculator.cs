using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.NewMexico;

/// <summary>
/// State module for New Mexico (NM) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// New Mexico Taxation and Revenue Department FYI-104 and
/// Form RPD-41272 (New Mexico Employee's Withholding Exemption Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction (mirrors the 2026 federal
///      standard deduction amounts as used by NM's withholding formula).
///   4. Subtract the RPD-41272 exemption deduction ($4,000 per exemption claimed).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply New Mexico's 2026 graduated income tax brackets.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form RPD-41272.
///
/// Filing statuses (per Form RPD-41272):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// 2026 New Mexico amounts (NM FYI-104 / NM SB 145 (2023) effective 2024+):
///   Standard deduction (mirrors 2026 federal):
///     Single / MFS:          $15,750
///     Married / QSS:         $31,500
///     Head of Household:     $23,625
///   Per-exemption deduction (RPD-41272 Line 5): $4,000 per exemption
///   Tax brackets:
///     Single / MFS:
///       1.7% on $0 – $5,500
///       3.2% on $5,500 – $11,000
///       4.7% on $11,000 – $16,000
///       4.9% on $16,000 – $210,000
///       5.9% over $210,000
///     Married / QSS and Head of Household:
///       1.7% on $0 – $8,000
///       3.2% on $8,000 – $16,000
///       4.7% on $16,000 – $24,000
///       4.9% on $24,000 – $315,000
///       5.9% over $315,000
///
/// Sources:
///   • New Mexico Taxation and Revenue Department, FYI-104, 2026.
///   • Form RPD-41272, New Mexico Employee's Withholding Exemption Certificate.
///   • NMSA 1978, Section 7-2-7 (individual income tax rates), as amended
///     by NM SB 145 (2023) effective tax year 2024.
/// </summary>
public sealed class NewMexicoWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 NM standard deduction for Single / Married Filing Separately
    /// (mirrors 2026 federal standard deduction for Single).</summary>
    public const decimal StandardDeductionSingle = 15_750m;

    /// <summary>2026 NM standard deduction for Married Filing Jointly / Qualifying Surviving Spouse
    /// (mirrors 2026 federal standard deduction for MFJ).</summary>
    public const decimal StandardDeductionMarried = 31_500m;

    /// <summary>2026 NM standard deduction for Head of Household
    /// (mirrors 2026 federal standard deduction for Head of Household).</summary>
    public const decimal StandardDeductionHeadOfHousehold = 23_625m;

    // ── Per-exemption deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per exemption claimed on Form RPD-41272.
    /// New Mexico uses $4,000 per personal exemption for tax year 2026.
    /// </summary>
    public const decimal ExemptionAmount = 4_000m;

    // ── Bracket ceiling thresholds — Single / MFS ────────────────────

    /// <summary>Upper ceiling of the first Single bracket ($5,500).</summary>
    public const decimal SingleBracket1Ceiling = 5_500m;

    /// <summary>Upper ceiling of the second Single bracket ($11,000).</summary>
    public const decimal SingleBracket2Ceiling = 11_000m;

    /// <summary>Upper ceiling of the third Single bracket ($16,000).</summary>
    public const decimal SingleBracket3Ceiling = 16_000m;

    /// <summary>Upper ceiling of the fourth Single bracket ($210,000). Income over this is taxed at 5.9%.</summary>
    public const decimal SingleBracket4Ceiling = 210_000m;

    // ── Bracket ceiling thresholds — Married / QSS and Head of Household ─

    /// <summary>Upper ceiling of the first Married / HoH bracket ($8,000).</summary>
    public const decimal MarriedBracket1Ceiling = 8_000m;

    /// <summary>Upper ceiling of the second Married / HoH bracket ($16,000).</summary>
    public const decimal MarriedBracket2Ceiling = 16_000m;

    /// <summary>Upper ceiling of the third Married / HoH bracket ($24,000).</summary>
    public const decimal MarriedBracket3Ceiling = 24_000m;

    /// <summary>Upper ceiling of the fourth Married / HoH bracket ($315,000). Income over this is taxed at 5.9%.</summary>
    public const decimal MarriedBracket4Ceiling = 315_000m;

    // ── Tax rates (shared across all filing statuses) ────────────────

    /// <summary>New Mexico rate for the first bracket (1.7%).</summary>
    public const decimal Rate1 = 0.017m;

    /// <summary>New Mexico rate for the second bracket (3.2%).</summary>
    public const decimal Rate2 = 0.032m;

    /// <summary>New Mexico rate for the third bracket (4.7%).</summary>
    public const decimal Rate3 = 0.047m;

    /// <summary>New Mexico rate for the fourth bracket (4.9%).</summary>
    public const decimal Rate4 = 0.049m;

    /// <summary>New Mexico top rate for the fifth bracket (5.9%).</summary>
    public const decimal Rate5 = 0.059m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public NewMexicoWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.NM, "FilingStatus");
    }

    public UsState State => UsState.NM;

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

        // Step 3: Subtract the filing-status standard deduction.
        var standardDeduction = filingStatus switch
        {
            StatusMarried         => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _                     => StandardDeductionSingle
        };

        // Step 4: Subtract RPD-41272 exemption deduction ($4,000 per exemption).
        var exemptionDeduction = exemptions * ExemptionAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - exemptionDeduction);

        // Step 6: Apply New Mexico's 2026 graduated brackets.
        //   Single / MFS:   1.7% on $0–$5,500; 3.2% on $5,500–$11,000;
        //                   4.7% on $11,000–$16,000; 4.9% on $16,000–$210,000;
        //                   5.9% over $210,000
        //   Married / HoH:  1.7% on $0–$8,000; 3.2% on $8,000–$16,000;
        //                   4.7% on $16,000–$24,000; 4.9% on $24,000–$315,000;
        //                   5.9% over $315,000
        var annualTax = filingStatus == StatusSingle
            ? ApplySingleBrackets(annualTaxableIncome)
            : ApplyMarriedBrackets(annualTaxableIncome);

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

    // ── Bracket helpers ───────────────────────────────────────────────

    private static decimal ApplySingleBrackets(decimal income)
    {
        // Single / MFS brackets:
        //   1.7% on $0 – $5,500
        //   3.2% on $5,500 – $11,000
        //   4.7% on $11,000 – $16,000
        //   4.9% on $16,000 – $210,000
        //   5.9% over $210,000
        if (income <= 0m) return 0m;
        return ComputeTax(income, SingleBracket1Ceiling, SingleBracket2Ceiling,
            SingleBracket3Ceiling, SingleBracket4Ceiling);
    }

    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // Married / QSS and Head of Household brackets:
        //   1.7% on $0 – $8,000
        //   3.2% on $8,000 – $16,000
        //   4.7% on $16,000 – $24,000
        //   4.9% on $24,000 – $315,000
        //   5.9% over $315,000
        if (income <= 0m) return 0m;
        return ComputeTax(income, MarriedBracket1Ceiling, MarriedBracket2Ceiling,
            MarriedBracket3Ceiling, MarriedBracket4Ceiling);
    }

    /// <summary>
    /// Applies New Mexico's five-rate bracket structure using the provided ceiling thresholds.
    /// Both filing-status bracket sets share the same five rates; only the thresholds differ.
    /// </summary>
    private static decimal ComputeTax(
        decimal income,
        decimal ceiling1,
        decimal ceiling2,
        decimal ceiling3,
        decimal ceiling4)
    {
        decimal tax = 0m;

        // 1.7% on $0 – ceiling1
        tax += Math.Min(income, ceiling1) * Rate1;

        // 3.2% on ceiling1 – ceiling2
        if (income > ceiling1)
            tax += (Math.Min(income, ceiling2) - ceiling1) * Rate2;

        // 4.7% on ceiling2 – ceiling3
        if (income > ceiling2)
            tax += (Math.Min(income, ceiling3) - ceiling2) * Rate3;

        // 4.9% on ceiling3 – ceiling4
        if (income > ceiling3)
            tax += (Math.Min(income, ceiling4) - ceiling3) * Rate4;

        // 5.9% over ceiling4
        if (income > ceiling4)
            tax += (income - ceiling4) * Rate5;

        return tax;
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
