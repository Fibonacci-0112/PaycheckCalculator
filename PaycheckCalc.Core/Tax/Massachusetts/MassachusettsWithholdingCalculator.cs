using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Massachusetts;

/// <summary>
/// State module for Massachusetts income tax withholding.
/// Implements the annualized percentage method per the Massachusetts Department
/// of Revenue (DOR) Employer's Tax Guide and Form M-4 (Employee's Withholding
/// Exemption Certificate).
///
/// Calculation steps (DOR annualized method):
///   1. Compute per-period taxable wages (gross − pre-tax deductions, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the personal exemption for the M-4 filing status.
///   4. Subtract dependent deductions ($1,000 × dependents on M-4).
///   5. Subtract blind-person exemptions ($2,200 × qualifying individuals).
///   6. Subtract age-65-or-over exemptions ($700 × qualifying individuals).
///   7. Apply the flat 5% rate to annual taxable income up to $1,000,000;
///      apply the 9% rate (5% + 4% surtax) to any excess above $1,000,000.
///   8. De-annualize (÷ pay periods per year) and round to two decimal places.
///   9. Add any additional per-period withholding the employee requested on
///      M-4 Line 4.
///
/// Filing statuses (per Form M-4):
///   • Single             — $4,400 personal exemption
///   • Married            — $8,800 personal exemption
///   • Head of Household  — $6,800 personal exemption
///
/// 2026 Massachusetts exemption amounts:
///   • Personal exemption:
///       Single:              $4,400
///       Married:             $8,800
///       Head of Household:   $6,800
///   • Dependent deduction:   $1,000 per dependent
///   • Blind exemption:       $2,200 per qualifying individual
///   • Age 65+ exemption:     $700 per qualifying individual
///   • Surtax threshold:      $1,000,000 (annual taxable income)
///   • Rate below threshold:  5%
///   • Rate above threshold:  9% (5% flat + 4% surtax per MA Question 1, 2022)
///
/// Sources:
///   • Massachusetts DOR, "2026 Massachusetts Income Tax Withholding Instructions",
///     Publication MW-1 / Employer's Tax Guide.
///   • Form M-4, "Employee's Withholding Exemption Certificate".
/// </summary>
public sealed class MassachusettsWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>Flat Massachusetts income tax rate (5%).</summary>
    private const decimal FlatRate = 0.05m;

    /// <summary>Additional surtax rate on annual taxable income above $1,000,000 (4%).</summary>
    private const decimal SurtaxRate = 0.04m;

    /// <summary>Annual taxable income threshold at which the 4% surtax begins.</summary>
    private const decimal SurtaxThreshold = 1_000_000m;

    /// <summary>Annual personal exemption for Single filing status (M-4).</summary>
    public const decimal PersonalExemptionSingle = 4_400m;

    /// <summary>Annual personal exemption for Married filing status (M-4).</summary>
    public const decimal PersonalExemptionMarried = 8_800m;

    /// <summary>Annual personal exemption for Head of Household filing status (M-4).</summary>
    public const decimal PersonalExemptionHeadOfHousehold = 6_800m;

    /// <summary>Annual deduction per dependent claimed on M-4.</summary>
    public const decimal DependentExemptionAmount = 1_000m;

    /// <summary>Annual exemption per qualifying blind individual (employee or spouse).</summary>
    public const decimal BlindExemptionAmount = 2_200m;

    /// <summary>Annual exemption per qualifying individual age 65 or over.</summary>
    public const decimal AgeExemptionAmount = 700m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public MassachusettsWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MA, "FilingStatus");
    }

    public UsState State => UsState.MA;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Dependents", 0) < 0)
            errors.Add("Dependents cannot be negative.");

        if (values.GetValueOrDefault("BlindExemptions", 0) < 0)
            errors.Add("Blind Exemptions cannot be negative.");

        if (values.GetValueOrDefault("AgeExemptions", 0) < 0)
            errors.Add("Age 65+ Exemptions cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus    = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var dependents      = Math.Max(0, values.GetValueOrDefault("Dependents", 0));
        var blindExemptions = Math.Max(0, values.GetValueOrDefault("BlindExemptions", 0));
        var ageExemptions   = Math.Max(0, values.GetValueOrDefault("AgeExemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period taxable wages (pre-tax deductions reduce state wages).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Personal exemption based on M-4 filing status.
        var personalExemption = filingStatus switch
        {
            StatusMarried         => PersonalExemptionMarried,
            StatusHeadOfHousehold => PersonalExemptionHeadOfHousehold,
            _                     => PersonalExemptionSingle
        };

        // Steps 4–6: Dependent, blind, and age-65+ exemptions.
        var totalExemption = personalExemption
            + (dependents      * DependentExemptionAmount)
            + (blindExemptions * BlindExemptionAmount)
            + (ageExemptions   * AgeExemptionAmount);

        // Annual taxable income floored at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages - totalExemption);

        // Step 7: Apply 5% flat rate; 9% on excess over $1,000,000 (4% surtax).
        var annualTax = ComputeAnnualTax(annualTaxableIncome);

        // Step 8: De-annualize and round to two decimal places.
        var withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        // Step 9: Add any per-period extra withholding from M-4 Line 4.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    // ── Tax computation ──────────────────────────────────────────────

    /// <summary>
    /// Computes the annual Massachusetts income tax.
    ///   5%  on annual taxable income up to $1,000,000.
    ///   9%  (5% + 4% surtax) on annual taxable income above $1,000,000.
    /// </summary>
    private static decimal ComputeAnnualTax(decimal annualTaxableIncome)
    {
        if (annualTaxableIncome <= 0m) return 0m;

        // 5% on the portion up to the surtax threshold.
        var belowThreshold = Math.Min(annualTaxableIncome, SurtaxThreshold);
        var tax = belowThreshold * FlatRate;

        // 9% on the portion above the surtax threshold.
        var aboveThreshold = Math.Max(0m, annualTaxableIncome - SurtaxThreshold);
        tax += aboveThreshold * (FlatRate + SurtaxRate);

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly    => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
