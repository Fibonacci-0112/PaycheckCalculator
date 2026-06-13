using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Georgia;

/// <summary>
/// State module for Georgia income tax withholding.
/// Implements the annualized percentage method per the Georgia Department of
/// Revenue 2026 Employer's Tax Guide and Form G-4 (Employee's Withholding
/// Allowance Certificate).
///
/// Calculation steps (G-4 / percentage method):
///   1. Compute per-period taxable wages (gross − pre-tax deductions).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the standard deduction for the filing status claimed on G-4.
///   4. Subtract the dependent allowance ($4,000 × dependents on G-4 line 4).
///   5. Subtract additional allowances claimed on G-4 line 5
///      ($3,000 × count — e.g. age 65+, blind, or estimated itemized
///      deductions in excess of the standard deduction).
///   6. Apply Georgia's flat 5.19% income tax rate (HB 111, effective for 2026).
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      G-4 line 6.
///
/// Filing statuses (per Form G-4 line 3):
///   • A — Single
///   • B — Married Filing Separately, OR Married Filing Jointly (both spouses
///         working) — each spouse files their own G-4
///   • C — Married Filing Jointly (one spouse working)
///   • D — Head of Household
///   • Exempt — employee claimed exempt on line 7 (no income tax withheld)
///
/// 2026 Georgia amounts (HB 111 / HB 1437):
///   • Flat income tax rate: 5.19%
///   • Standard deduction:
///       Status A (Single):              $12,000
///       Status B (MFS / MFJ both work):  $12,000
///       Status C (MFJ one-spouse):       $24,000
///       Status D (Head of Household):    $12,000
///   • Dependent allowance: $4,000 per dependent
///   • Additional allowance: $3,000 per qualifying factor
///
/// Sources:
///   • Georgia Department of Revenue, 2026 Employer's Tax Guide
///     <see href="https://dor.georgia.gov/employers-tax-guide"/>
///   • Form G-4, Employee's Withholding Allowance Certificate
///     <see href="https://dor.georgia.gov/document/form/tsdemployeeswithholdingallowancecertificateg-4pdf/download"/>
///   • Georgia HB 111 (2025) — flat 5.19% rate effective tax year 2025, with
///     scheduled 0.10-percentage-point annual reductions subject to revenue
///     triggers. The 2026 reduction to 5.09% has not been certified as of
///     publication of the 2026 Employer's Tax Guide, so employers withhold
///     at 5.19%.
/// </summary>
public sealed class GeorgiaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>Georgia flat income tax rate for 2026 (5.19%).</summary>
    public const decimal FlatRate = 0.0519m;

    /// <summary>Standard deduction for filing status A, B, and D.</summary>
    public const decimal StandardDeductionSingle = 12_000m;

    /// <summary>Standard deduction for filing status C (MFJ, one spouse working).</summary>
    public const decimal StandardDeductionMarriedJointOneWorking = 24_000m;

    /// <summary>
    /// Annual deduction per dependent claimed on G-4 line 4.
    /// Subtracted from annual taxable wages (not from computed tax).
    /// </summary>
    public const decimal DependentAllowance = 4_000m;

    /// <summary>
    /// Annual deduction per qualifying additional allowance claimed on
    /// G-4 line 5 (age 65+, blind, or each $3,000 of estimated itemized
    /// deductions in excess of the standard deduction).
    /// </summary>
    public const decimal AdditionalAllowanceAmount = 3_000m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusA = "A — Single";
    public const string StatusB = "B — Married Filing Separately / Both Spouses Working";
    public const string StatusC = "C — Married Filing Jointly, One Spouse Working";
    public const string StatusD = "D — Head of Household";
    public const string StatusExempt = "Exempt";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public GeorgiaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.GA, "FilingStatus");
    }

    public UsState State => UsState.GA;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Dependents", 0) < 0)
            errors.Add("Dependents cannot be negative.");

        if (values.GetValueOrDefault("AdditionalAllowances", 0) < 0)
            errors.Add("Additional Allowances cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", StatusA);
        var dependents = Math.Max(0, values.GetValueOrDefault("Dependents", 0));
        var additionalAllowances = Math.Max(0, values.GetValueOrDefault("AdditionalAllowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period taxable wages (gross minus state-wage-reducing pre-tax deductions).
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        // Exempt employees (G-4 line 7) have no income tax withheld, though
        // we still report the pay period's taxable wages for transparency.
        if (filingStatus == StatusExempt)
        {
            return new StateWithholdingResult
            {
                TaxableWages = taxableWages,
                Withholding = 0m,
                Description = "Exempt per G-4 line 7"
            };
        }

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the standard deduction for the G-4 filing status.
        var standardDeduction = filingStatus == StatusC
            ? StandardDeductionMarriedJointOneWorking
            : StandardDeductionSingle;

        // Step 4–5: Subtract dependent and additional allowances.
        var allowances =
            (dependents * DependentAllowance) +
            (additionalAllowances * AdditionalAllowanceAmount);

        var annualTaxableIncome = Math.Max(0m, annualWages - standardDeduction - allowances);

        // Step 6: Apply the flat rate.
        var annualTax = annualTaxableIncome * FlatRate;

        // Step 7: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add any per-period extra withholding requested on G-4 line 6.
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
