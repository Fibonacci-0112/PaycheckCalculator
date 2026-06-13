using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Louisiana;

/// <summary>
/// State module for Louisiana income tax withholding.
/// Implements the annualized percentage method per the Louisiana Department of
/// Revenue Withholding Tables and Formulas (R-1306) and Form L-4 (Employee's
/// Withholding Exemption Certificate).
///
/// Calculation steps (R-1306 percentage-method formula):
///   1. Compute per-period taxable wages (gross − pre-tax deductions).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the personal exemption for the filing status claimed on L-4.
///   4. Subtract the dependent deduction ($1,000 × number of dependents on
///      L-4 Line 6B).
///   5. Apply Louisiana's graduated income tax brackets to the annual taxable
///      income.
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested on
///      L-4 Line 7.
///
/// Filing statuses (per Form L-4):
///   • Single             — $4,500 personal exemption; single brackets
///   • Married            — $9,000 personal exemption; married brackets
///   • Head of Household  — $9,000 personal exemption; married brackets
///
/// 2026 Louisiana amounts (R-1306):
///   • Personal exemption:
///       Single:                        $4,500
///       Married / Head of Household:   $9,000
///   • Dependent deduction: $1,000 per dependent (L-4 Line 6B)
///   • Graduated brackets — Single:
///       1.85% on $0–$12,500
///       3.50% on $12,501–$50,000
///       4.25% on over $50,000
///   • Graduated brackets — Married / Head of Household:
///       1.85% on $0–$25,000
///       3.50% on $25,001–$100,000
///       4.25% on over $100,000
///
/// Sources:
///   • Louisiana Department of Revenue, R-1306 (Withholding Tables and
///     Formulas), current edition.
///     <see href="https://revenue.louisiana.gov/Publications/R-1306(1_20).pdf"/>
///   • Form L-4, Employee's Withholding Exemption Certificate.
///     <see href="https://revenue.louisiana.gov/TaxForms/L-4(1_07).pdf"/>
/// </summary>
public sealed class LouisianaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>Personal exemption for Single filing status.</summary>
    public const decimal PersonalExemptionSingle = 4_500m;

    /// <summary>Personal exemption for Married and Head of Household filing statuses.</summary>
    public const decimal PersonalExemptionMarried = 9_000m;

    /// <summary>Annual deduction per dependent claimed on L-4 Line 6B.</summary>
    public const decimal DependentDeduction = 1_000m;

    // Single brackets
    private const decimal SingleBracket1Ceiling = 12_500m;
    private const decimal SingleBracket2Ceiling = 50_000m;
    private const decimal Rate1 = 0.0185m;
    private const decimal Rate2 = 0.035m;
    private const decimal Rate3 = 0.0425m;

    // Married / Head of Household brackets
    private const decimal MarriedBracket1Ceiling = 25_000m;
    private const decimal MarriedBracket2Ceiling = 100_000m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public LouisianaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.LA, "FilingStatus");
    }

    public UsState State => UsState.LA;

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

        // Step 1: Per-period taxable wages (gross minus state-wage-reducing pre-tax deductions).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the personal exemption for the filing status.
        var personalExemption = filingStatus == StatusSingle
            ? PersonalExemptionSingle
            : PersonalExemptionMarried;

        // Step 4: Subtract the dependent deduction ($1,000 per dependent).
        var dependentTotal = dependents * DependentDeduction;

        var annualTaxableIncome = Math.Max(0m, annualWages - personalExemption - dependentTotal);

        // Step 5: Apply graduated brackets to annual taxable income.
        var annualTax = filingStatus == StatusSingle
            ? CalculateSingleTax(annualTaxableIncome)
            : CalculateMarriedTax(annualTaxableIncome);

        // Step 6: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding requested on L-4 Line 7.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    // ── Tax bracket computations ─────────────────────────────────────

    /// <summary>
    /// Applies the Louisiana Single graduated brackets to annual taxable income.
    ///   1.85% on $0–$12,500
    ///   3.50% on $12,501–$50,000
    ///   4.25% on over $50,000
    /// </summary>
    private static decimal CalculateSingleTax(decimal annualTaxableIncome)
    {
        if (annualTaxableIncome <= 0m) return 0m;

        // Bracket 1: 1.85% on $0–$12,500
        var bracket1 = Math.Min(annualTaxableIncome, SingleBracket1Ceiling) * Rate1;

        // Bracket 2: 3.50% on $12,501–$50,000
        var bracket2 = Math.Max(0m, Math.Min(annualTaxableIncome, SingleBracket2Ceiling) - SingleBracket1Ceiling) * Rate2;

        // Bracket 3: 4.25% on over $50,000
        var bracket3 = Math.Max(0m, annualTaxableIncome - SingleBracket2Ceiling) * Rate3;

        return bracket1 + bracket2 + bracket3;
    }

    /// <summary>
    /// Applies the Louisiana Married / Head of Household graduated brackets to
    /// annual taxable income.
    ///   1.85% on $0–$25,000
    ///   3.50% on $25,001–$100,000
    ///   4.25% on over $100,000
    /// </summary>
    private static decimal CalculateMarriedTax(decimal annualTaxableIncome)
    {
        if (annualTaxableIncome <= 0m) return 0m;

        // Bracket 1: 1.85% on $0–$25,000
        var bracket1 = Math.Min(annualTaxableIncome, MarriedBracket1Ceiling) * Rate1;

        // Bracket 2: 3.50% on $25,001–$100,000
        var bracket2 = Math.Max(0m, Math.Min(annualTaxableIncome, MarriedBracket2Ceiling) - MarriedBracket1Ceiling) * Rate2;

        // Bracket 3: 4.25% on over $100,000
        var bracket3 = Math.Max(0m, annualTaxableIncome - MarriedBracket2Ceiling) * Rate3;

        return bracket1 + bracket2 + bracket3;
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
