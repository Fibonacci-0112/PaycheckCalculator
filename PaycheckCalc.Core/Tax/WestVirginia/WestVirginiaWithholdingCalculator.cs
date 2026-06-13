using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.WestVirginia;

/// <summary>
/// State module for West Virginia income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// West Virginia State Tax Department <em>Employee's Withholding Exemption
/// Certificate</em> (Form IT-104, 2026).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract allowance deductions (exemptions × $2,000).
///   4. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   5. Apply West Virginia's graduated income tax brackets to annual taxable income.
///      (All filing statuses share the same bracket thresholds.)
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested on
///      Form IT-104.
///
/// Filing statuses (per Form IT-104):
///   • Single  — Single filers (higher withholding rate).
///   • Married — Married filers.
///   West Virginia does not define a separate Head of Household withholding status.
///
/// 2026 West Virginia amounts (WV State Tax Dept., Form IT-104, 2026):
///   Standard deduction: none (West Virginia does not apply a state standard deduction
///                        to the withholding formula).
///   Per-exemption deduction: $2,000 (claimed on Form IT-104)
///   Brackets (all filing statuses share the same bracket thresholds):
///     3.00% on $0        – $10,000
///     4.00% on $10,001   – $25,000
///     4.50% on $25,001   – $40,000
///     6.00% on $40,001   – $60,000
///     6.50% over $60,000
///
/// Sources:
///   • West Virginia State Tax Department, <em>Employee's Withholding Exemption
///     Certificate</em> (Form IT-104), effective 2026.
///   • West Virginia Code § 11-21-71 (income tax brackets and rates).
/// </summary>
public sealed class WestVirginiaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Exemption ────────────────────────────────────────────────────

    /// <summary>Annual deduction per personal exemption claimed on Form IT-104 (2026).</summary>
    public const decimal ExemptionAmount = 2_000m;

    // ── Bracket thresholds (all filing statuses use the same brackets) ──

    /// <summary>Upper bound of the first (3.00%) bracket.</summary>
    public const decimal Bracket1Ceiling = 10_000m;

    /// <summary>Upper bound of the second (4.00%) bracket.</summary>
    public const decimal Bracket2Ceiling = 25_000m;

    /// <summary>Upper bound of the third (4.50%) bracket.</summary>
    public const decimal Bracket3Ceiling = 40_000m;

    /// <summary>Upper bound of the fourth (6.00%) bracket.</summary>
    public const decimal Bracket4Ceiling = 60_000m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>West Virginia income tax rate for the first bracket (3.00%).</summary>
    public const decimal Rate1 = 0.03m;

    /// <summary>West Virginia income tax rate for the second bracket (4.00%).</summary>
    public const decimal Rate2 = 0.04m;

    /// <summary>West Virginia income tax rate for the third bracket (4.50%).</summary>
    public const decimal Rate3 = 0.045m;

    /// <summary>West Virginia income tax rate for the fourth bracket (6.00%).</summary>
    public const decimal Rate4 = 0.06m;

    /// <summary>West Virginia income tax rate for the top bracket (6.50%).</summary>
    public const decimal Rate5 = 0.065m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public WestVirginiaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.WV, "FilingStatus");
    }

public UsState State => UsState.WV;


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
        var exemptions = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract personal exemptions ($2,000 each).
        // West Virginia has no state standard deduction in the withholding formula.
        annualWages -= exemptions * ExemptionAmount;

        // Step 4: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages);

        // Step 5: Apply graduated brackets (all filing statuses share the same thresholds).
        var annualTax = ApplyBrackets(annualTaxableIncome);

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

    // ── Bracket helper ────────────────────────────────────────────────

    private static decimal ApplyBrackets(decimal income)
    {
        // West Virginia graduated brackets (identical for all filing statuses):
        //   3.00% on $0       – $10,000
        //   4.00% on $10,001  – $25,000
        //   4.50% on $25,001  – $40,000
        //   6.00% on $40,001  – $60,000
        //   6.50% over $60,000
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        var tier1 = Math.Min(income, Bracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > Bracket1Ceiling)
        {
            var tier2 = Math.Min(income, Bracket2Ceiling) - Bracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > Bracket2Ceiling)
        {
            var tier3 = Math.Min(income, Bracket3Ceiling) - Bracket2Ceiling;
            tax += tier3 * Rate3;
        }

        if (income > Bracket3Ceiling)
        {
            var tier4 = Math.Min(income, Bracket4Ceiling) - Bracket3Ceiling;
            tax += tier4 * Rate4;
        }

        if (income > Bracket4Ceiling)
        {
            var tier5 = income - Bracket4Ceiling;
            tax += tier5 * Rate5;
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
