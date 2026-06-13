using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Utah;

/// <summary>
/// State module for Utah (UT) income tax withholding.
/// Utah does not have its own withholding certificate; employers use the
/// employee's federal W-4 information together with the Utah Publication 14
/// withholding tables to determine state income tax withholding.
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Compute annual gross tax = annual wages × 4.5%.
///   4. Compute the net allowance credit (phase-out applies):
///      a. Gross credit = allowances × base credit per allowance
///            ($450 for Single / $900 for Married).
///      b. Phase-out reduction = max(0, annual wages − threshold) × 1.3%
///            (threshold: $9,107 Single / $18,213 Married).
///      c. Net credit = max(0, gross credit − phase-out reduction).
///   5. Annual withholding = max(0, annual gross tax − net credit).
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested.
///
/// Filing statuses (per federal W-4):
///   • Single  — Single or Married Filing Separately.
///   • Married — Married Filing Jointly or Qualifying Surviving Spouse.
///
/// 2026 Utah amounts (Utah State Tax Commission, Publication 14):
///   Flat income tax rate: 4.5%
///   Allowance base credit per allowance:
///     Single:  $450
///     Married: $900
///   Allowance phase-out threshold (start of phase-out):
///     Single:  $9,107  in annual wages
///     Married: $18,213 in annual wages
///   Phase-out rate: 1.3% of excess annual wages above the threshold
///
/// Sources:
///   • Utah State Tax Commission, Publication 14: Withholding Tax Guide (2026).
///   • Utah Code §59-10-516 (flat income tax rate).
///   • Federal W-4: used for Utah withholding in lieu of a state form.
/// </summary>
public sealed class UtahWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Flat tax rate ────────────────────────────────────────────────

    /// <summary>2026 Utah flat income tax rate (4.5%).</summary>
    public const decimal TaxRate = 0.045m;

    // ── Allowance base credits ───────────────────────────────────────

    /// <summary>
    /// Annual base allowance credit per allowance for Single / MFS filers ($450).
    /// Applied to computed annual tax before phase-out reduction.
    /// </summary>
    public const decimal AllowanceCreditSingle = 450m;

    /// <summary>
    /// Annual base allowance credit per allowance for Married / QSS filers ($900).
    /// Applied to computed annual tax before phase-out reduction.
    /// </summary>
    public const decimal AllowanceCreditMarried = 900m;

    // ── Phase-out thresholds ─────────────────────────────────────────

    /// <summary>
    /// Annual wage threshold above which the allowance credit begins to phase out
    /// for Single / MFS filers ($9,107).
    /// </summary>
    public const decimal PhaseOutThresholdSingle = 9_107m;

    /// <summary>
    /// Annual wage threshold above which the allowance credit begins to phase out
    /// for Married / QSS filers ($18,213).
    /// </summary>
    public const decimal PhaseOutThresholdMarried = 18_213m;

    // ── Phase-out rate ───────────────────────────────────────────────

    /// <summary>
    /// Rate applied to excess annual wages above the phase-out threshold to
    /// compute the reduction to the gross allowance credit (1.3%).
    /// </summary>
    public const decimal PhaseOutRate = 0.013m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle  = "Single";
    public const string StatusMarried = "Married";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public UtahWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.UT, "FilingStatus");
    }

public UsState State => UsState.UT;


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

        // Step 3: Annual gross tax at the flat 4.5% rate.
        var annualGrossTax = annualWages * TaxRate;

        // Step 4: Compute the net allowance credit (phase-out reduces the credit
        //         when annual wages exceed the filing-status threshold).
        decimal baseCredit = filingStatus == StatusMarried
            ? AllowanceCreditMarried
            : AllowanceCreditSingle;

        decimal phaseOutThreshold = filingStatus == StatusMarried
            ? PhaseOutThresholdMarried
            : PhaseOutThresholdSingle;

        // 4a. Gross credit before phase-out.
        var grossCredit = allowances * baseCredit;

        // 4b. Phase-out reduction: 1.3% of wages above the threshold.
        var excessWages    = Math.Max(0m, annualWages - phaseOutThreshold);
        var phaseOutAmount = excessWages * PhaseOutRate;

        // 4c. Net credit — cannot go below zero.
        var netCredit = Math.Max(0m, grossCredit - phaseOutAmount);

        // Step 5: Annual withholding — cannot go below zero.
        var annualWithholding = Math.Max(0m, annualGrossTax - netCredit);

        // Step 6: De-annualize and round to two decimal places.
        var periodTax   = annualWithholding / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding  = withholding
        };
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
