using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Mississippi;

/// <summary>
/// State module for Mississippi income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Mississippi Department of Revenue Employer's Withholding Tax Instructions
/// and Form 89-350 (Employee's Withholding Exemption Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract the filing-status personal exemption.
///   5. Subtract the dependent exemption ($1,500 × number of dependents).
///   6. Low-income exemption: floor annual taxable income at zero.
///   7. Apply Mississippi's 2026 income tax brackets to annual taxable income.
///   8. De-annualize (÷ pay periods per year) and round to two decimal places.
///   9. Add any additional per-period withholding the employee requested on
///      Form 89-350.
///
/// Filing statuses (per Form 89-350):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly.
///   • Head of Household — Head of Household.
///
/// 2026 Mississippi amounts (MS DOR Employer's Withholding Tax Instructions):
///   Standard deduction:  $2,300 (Single) / $4,600 (Married) / $3,400 (Head of Household)
///   Personal exemption:  $6,000 (Single) / $12,000 (Married) / $9,500 (Head of Household)
///   Dependent exemption: $1,500 per dependent (Form 89-350 Line 6)
///   Tax brackets (all filing statuses):
///     0%  on $0 – $10,000
///     4%  over $10,000
///
/// Sources:
///   • Mississippi Department of Revenue, Employer's Withholding Tax Instructions
///     (Publication 89-105), 2026.
///   • Form 89-350, Employee's Withholding Exemption Certificate.
///   • MS HB 531 (2022): eliminated the 4% bracket on $5,001–$10,000.
///   • MS HB 1 (2023): phase-down of top rate from 5% to 4% by tax year 2026.
/// </summary>
public sealed class MississippiWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>2026 Mississippi standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 2_300m;

    /// <summary>2026 Mississippi standard deduction for Married Filing Jointly.</summary>
    public const decimal StandardDeductionMarried = 4_600m;

    /// <summary>2026 Mississippi standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 3_400m;

    /// <summary>2026 Mississippi personal exemption for Single / Married Filing Separately.</summary>
    public const decimal PersonalExemptionSingle = 6_000m;

    /// <summary>2026 Mississippi personal exemption for Married Filing Jointly.</summary>
    public const decimal PersonalExemptionMarried = 12_000m;

    /// <summary>2026 Mississippi personal exemption for Head of Household.</summary>
    public const decimal PersonalExemptionHeadOfHousehold = 9_500m;

    /// <summary>Annual deduction per dependent claimed on Form 89-350 Line 6.</summary>
    public const decimal DependentExemption = 1_500m;

    // ── Tax bracket thresholds and rates ────────────────────────────

    /// <summary>
    /// Upper bound of the zero-rate bracket.  The first $10,000 of annual
    /// taxable income is taxed at 0% (MS HB 531, 2022 / HB 1, 2023).
    /// </summary>
    public const decimal ZeroRateCeiling = 10_000m;

    /// <summary>Mississippi income tax rate applied to annual taxable income over $10,000 (2026).</summary>
    public const decimal Rate = 0.04m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MississippiWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MS, "FilingStatus");
    }

    public UsState State => UsState.MS;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Dependents", 0) < 0)
            errors.Add("Dependents cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var dependents = Math.Max(0, values.GetValueOrDefault("Dependents", 0));
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

        // Step 4: Subtract the filing-status personal exemption.
        var personalExemption = filingStatus switch
        {
            StatusMarried => PersonalExemptionMarried,
            StatusHeadOfHousehold => PersonalExemptionHeadOfHousehold,
            _ => PersonalExemptionSingle
        };

        // Step 5: Subtract the dependent exemption ($1,500 per dependent).
        var dependentTotal = dependents * DependentExemption;

        // Step 6: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - personalExemption - dependentTotal);

        // Step 7: Apply 2026 Mississippi brackets.
        //   0% on $0–$10,000
        //   4% on income over $10,000
        var annualTax = Math.Max(0m, annualTaxableIncome - ZeroRateCeiling) * Rate;

        // Step 8: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 9: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
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
