using PaycheckCalc.Core.Explanation;

namespace PaycheckCalc.App.Models;

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

    /// <summary>
    /// "Show Your Work" breakdown for each visible paycheck line, used by the
    /// info-icon modals on the results page. Defaults to an empty aggregate.
    /// </summary>
    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}
