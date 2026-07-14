using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.App.Models;

/// <summary>
/// Presentation model for displaying paycheck results in the UI.
/// Decouples the view layer from the domain <c>PaycheckResult</c> type,
/// adding display-specific computed properties the XAML can bind to directly.
/// </summary>
public sealed class ResultCardModel
{
    // ── Income ──────────────────────────────────────────────
    public decimal GrossPay { get; init; }
    public decimal FederalTaxableIncome { get; init; }
    public decimal FicaTaxableWages { get; init; }
    public decimal StateTaxableWages { get; init; }

    // ── Tax withholdings ────────────────────────────────────
    public decimal FederalWithholding { get; init; }
    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }

    // ── Deductions ──────────────────────────────────────────
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    // ── Totals ──────────────────────────────────────────────
    public decimal TotalTaxes { get; init; }
    public decimal NetPay { get; init; }

    // ── Gross-up (only populated when the result came from a gross-up) ──
    /// <summary>True when this result was produced by the gross-up calculator.</summary>
    public bool IsGrossUp { get; init; }

    /// <summary>The desired net (take-home) pay the gross-up targeted.</summary>
    public decimal TargetNetPay { get; init; }

    /// <summary>Extra gross beyond the target net that covers taxes and deductions (GrossPay − TargetNetPay).</summary>
    public decimal GrossUpCost { get; init; }

    // ── Tax year ────────────────────────────────────────────
    /// <summary>The tax year whose tables were used to produce this result (e.g. 2026).</summary>
    public int TaxYear { get; init; }

    // ── Bonus / supplemental wage (only populated for a bonus result) ──
    /// <summary>True when this result was produced by the bonus / supplemental-wage calculator.</summary>
    public bool IsBonus { get; init; }

    /// <summary>
    /// True when the work state has no flat supplemental rate, so <see cref="StateWithholding"/>
    /// is 0 and the net shown is before state income tax.
    /// </summary>
    public bool BonusStateUsesRegularMethod { get; init; }

    /// <summary>Human-readable summary of how the bonus's state withholding was derived (or why it is 0).</summary>
    public string BonusStateDescription { get; init; } = "";

    // ── Self-employment / 1099 (only populated for a self-employment result) ──
    /// <summary>True when this result was produced by the self-employment / 1099 calculator.</summary>
    public bool IsSelfEmployment { get; init; }

    /// <summary>
    /// The quarterly estimated-payment schedule (Form 1040-ES) for a self-employment result.
    /// Empty for every other result type.
    /// </summary>
    public IReadOnlyList<QuarterlyEstimate> QuarterlyEstimates { get; init; } = Array.Empty<QuarterlyEstimate>();

    // ── Display helpers (UI-only concerns) ──────────────────
    /// <summary>True when state disability insurance is non-zero and should be shown.</summary>
    public bool ShowStateDisabilityInsurance => StateDisabilityInsurance > 0;

    /// <summary>
    /// Display label for the disability-insurance line item.
    /// Varies by state (e.g., "State Disability Insurance" for CA, "Family Leave Insurance" for CT).
    /// </summary>
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    /// <summary>Human-readable state name for display (e.g., "California").</summary>
    public string StateName { get; init; } = "";

    /// <summary>The tax year this result was calculated under (e.g. 2026).</summary>
    public int TaxYear { get; init; } = TaxYearSupport.Default;

    /// <summary>
    /// "Show Your Work" breakdown for each visible paycheck line, used by the
    /// info-icon modals on the results page. Defaults to an empty aggregate.
    /// </summary>
    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}
