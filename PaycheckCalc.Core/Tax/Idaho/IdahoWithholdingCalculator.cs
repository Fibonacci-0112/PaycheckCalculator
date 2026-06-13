using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Idaho;

/// <summary>
/// State module for Idaho income tax withholding.
/// Implements the annualized percentage-method / "computer formula" described
/// in the Idaho State Tax Commission publication EPB00006,
/// <em>A Guide to Idaho Income Tax Withholding</em>.
///
/// Calculation steps (EPB00006, Computer Formula):
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction. Idaho conforms to the
///      federal standard deduction per Idaho Code § 63-3022.
///   4. Subtract ID W-4 allowance amounts (pre-2020 W-4 holdover):
///      $3,300 per allowance claimed.
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Idaho's flat 5.3% income tax rate (HB 521, 2024) to annual
///      taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form ID W-4.
///
/// Filing statuses (per Form ID W-4):
///   • Single — includes Head of Household and Married Filing Separately;
///     Idaho withholding uses the single-size standard deduction for all
///     three federal statuses.
///   • Married — Married Filing Jointly (single-earner treatment on ID W-4).
///
/// 2026 Idaho amounts:
///   • Flat income tax rate: 5.3% (Idaho HB 521, effective tax year 2024+)
///   • Standard deduction (conforms to federal 2026):
///       Single / HoH / MFS:        $16,100
///       Married Filing Jointly:    $32,200
///   • Allowance amount:            $3,300 per ID W-4 allowance
///
/// Sources:
///   • Idaho State Tax Commission, EPB00006 <em>A Guide to Idaho Income Tax
///     Withholding</em>.
///   • Idaho HB 521 (2024) — flat 5.3% individual and corporate income tax
///     rate effective tax year 2024 and following.
///   • IRS Rev. Proc. 2025-32 (federal 2026 standard deductions, to which
///     Idaho conforms per Idaho Code § 63-3022).
/// </summary>
public sealed class IdahoWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>Idaho flat income tax rate for 2026 (5.3%).</summary>
    public const decimal FlatRate = 0.053m;

    /// <summary>2026 Idaho standard deduction for Single / HoH / MFS.</summary>
    public const decimal StandardDeductionSingle = 16_100m;

    /// <summary>2026 Idaho standard deduction for Married Filing Jointly.</summary>
    public const decimal StandardDeductionMarried = 32_200m;

    /// <summary>
    /// Annual deduction per ID W-4 allowance (pre-2020 W-4 holdover).
    /// Subtracted from annual taxable wages after the standard deduction.
    /// </summary>
    public const decimal AllowanceAmount = 3_300m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public IdahoWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.ID, "FilingStatus");
    }

    public UsState State => UsState.ID;

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

        // Step 3: Subtract standard deduction for the ID W-4 filing status.
        var standardDeduction = filingStatus == StatusMarried
            ? StandardDeductionMarried
            : StandardDeductionSingle;

        // Step 4: Subtract allowances.
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply the flat rate.
        var annualTax = annualTaxableIncome * FlatRate;

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
