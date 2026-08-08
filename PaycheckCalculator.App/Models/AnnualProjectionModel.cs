using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.App.Models;

/// <summary>
/// Presentation-ready projection card for the MAUI Results page's <b>Annual</b> sub-tab.
/// Mirrors the domain <see cref="Core.Models.AnnualProjection"/> but adds display-only
/// helpers the XAML can bind to directly (visibility flags, refund/owe state, a
/// progress label), keeping the view free of the raw domain type and of converters.
/// </summary>
public sealed class AnnualProjectionModel
{
    // ── Pay period info ─────────────────────────────────────
    public int PayPeriodsPerYear { get; init; }
    public int CurrentPaycheckNumber { get; init; }
    public int RemainingPaychecks { get; init; }

    // ── Annualized amounts ──────────────────────────────────
    public decimal AnnualizedGrossPay { get; init; }
    public decimal AnnualizedPreTaxDeductions { get; init; }
    public decimal AnnualizedFederalWithholding { get; init; }
    public decimal AnnualizedStateWithholding { get; init; }
    public decimal AnnualizedFica { get; init; }
    public decimal AnnualizedNetPay { get; init; }

    // ── Projected year-to-date ──────────────────────────────
    public decimal ProjectedYtdGrossPay { get; init; }
    public decimal ProjectedYtdFederalWithholding { get; init; }
    public decimal ProjectedYtdStateWithholding { get; init; }
    public decimal ProjectedYtdFica { get; init; }
    public decimal ProjectedYtdNetPay { get; init; }

    // ── Year-end estimate ───────────────────────────────────
    public decimal EstimatedAnnualFederalLiability { get; init; }
    public decimal EstimatedAnnualFicaLiability { get; init; }
    public decimal EstimatedTotalLiability { get; init; }
    public decimal AnnualizedTotalWithholding { get; init; }
    public decimal OverUnderWithholding { get; init; }
    public IReadOnlyList<AccuracyNote> AccuracyNotes { get; init; } = Array.Empty<AccuracyNote>();

    // ── Display helpers (UI-only concerns) ──────────────────
    /// <summary>True when annualized pre-tax deductions are non-zero and worth showing.</summary>
    public bool ShowAnnualizedPreTaxDeductions => AnnualizedPreTaxDeductions > 0m;

    /// <summary>True when withholding exceeds estimated liability (likely refund).</summary>
    public bool IsRefund => OverUnderWithholding > 0m;

    /// <summary>True when withholding falls short of estimated liability (likely owe).</summary>
    public bool IsOwe => OverUnderWithholding < 0m;

    /// <summary>True when projected withholding matches estimated liability exactly.</summary>
    public bool IsBalanced => OverUnderWithholding == 0m;

    /// <summary>Magnitude of the over/under figure, shown alongside a refund/owe label.</summary>
    public decimal OverUnderAbsolute => Math.Abs(OverUnderWithholding);
    public bool HasAccuracyNotes => AccuracyNotes.Count > 0;

    /// <summary>"Paycheck 3 of 26"-style progress label for the projected-YTD card.</summary>
    public string PaycheckProgressLabel => $"Paycheck {CurrentPaycheckNumber} of {PayPeriodsPerYear}";
}
