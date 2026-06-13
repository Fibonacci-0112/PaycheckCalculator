using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.RhodeIsland;

/// <summary>
/// State module for Rhode Island (RI) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Rhode Island Division of Taxation employer withholding guide and
/// Form RI W-4 (Employee's Withholding Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the standard deduction ($10,550 for all filing statuses).
///   4. Subtract the RI W-4 exemption deduction ($4,700 per exemption claimed).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply Rhode Island's 2026 graduated income tax brackets.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form RI W-4.
///
/// Filing statuses (per Form RI W-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// Note: Rhode Island uses the same standard deduction ($10,550) and the same
/// graduated brackets regardless of filing status.  The filing status is
/// collected on Form RI W-4 but does not alter the computation.
///
/// 2026 Rhode Island amounts (RI Division of Taxation Pub. T-174):
///   Standard deduction (all filing statuses): $10,550
///   Per-exemption deduction (RI W-4 Line 2):  $4,700
///   Tax brackets (all filing statuses):
///     3.75% on $0 – $77,450
///     4.75% on $77,450 – $176,050
///     5.99% over $176,050
///
/// Sources:
///   • Rhode Island Division of Taxation, 2026 Employer's Tax Calendar and
///     Withholding Guide (Pub. T-174).
///   • Form RI W-4, Employee's Withholding Certificate.
/// </summary>
public sealed class RhodeIslandWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deduction (same for all filing statuses) ────────────

    /// <summary>2026 RI standard deduction — applies equally to all filing statuses.</summary>
    public const decimal StandardDeduction = 10_550m;

    // ── Per-exemption deduction ───────────────────────────────────────

    /// <summary>
    /// Annual deduction per exemption claimed on Form RI W-4 Line 2.
    /// Rhode Island uses $4,700 per exemption for tax year 2026.
    /// </summary>
    public const decimal ExemptionAmount = 4_700m;

    // ── Bracket ceiling thresholds (shared across all filing statuses) ─

    /// <summary>Upper ceiling of the first tax bracket ($77,450).</summary>
    public const decimal Bracket1Ceiling = 77_450m;

    /// <summary>Upper ceiling of the second tax bracket ($176,050). Income over this is taxed at 5.99%.</summary>
    public const decimal Bracket2Ceiling = 176_050m;

    // ── Tax rates ─────────────────────────────────────────────────────

    /// <summary>Rhode Island rate for the first bracket (3.75%).</summary>
    public const decimal Rate1 = 0.0375m;

    /// <summary>Rhode Island rate for the second bracket (4.75%).</summary>
    public const decimal Rate2 = 0.0475m;

    /// <summary>Rhode Island top rate for the third bracket (5.99%).</summary>
    public const decimal Rate3 = 0.0599m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public RhodeIslandWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.RI, "FilingStatus");
    }

public UsState State => UsState.RI;


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
        var exemptions       = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the standard deduction ($10,550 for all filing statuses).
        // Step 4: Subtract the RI W-4 exemption deduction ($4,700 per exemption).
        var exemptionDeduction = exemptions * ExemptionAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - StandardDeduction - exemptionDeduction);

        // Step 6: Apply Rhode Island's 2026 graduated brackets.
        // All filing statuses use the same bracket structure.
        var annualTax = ApplyBrackets(annualTaxableIncome);

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

    // ── Bracket helper ────────────────────────────────────────────────

    private static decimal ApplyBrackets(decimal income)
    {
        // Rhode Island 2026 graduated brackets (same for all filing statuses):
        //   3.75% on $0 – $77,450
        //   4.75% on $77,450 – $176,050
        //   5.99% over $176,050
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        // 3.75% on $0 – $77,450
        tax += Math.Min(income, Bracket1Ceiling) * Rate1;

        // 4.75% on $77,450 – $176,050
        if (income > Bracket1Ceiling)
            tax += (Math.Min(income, Bracket2Ceiling) - Bracket1Ceiling) * Rate2;

        // 5.99% over $176,050
        if (income > Bracket2Ceiling)
            tax += (income - Bracket2Ceiling) * Rate3;

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
