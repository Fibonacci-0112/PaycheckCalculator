using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Tax.Supplemental;

namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Result of a supplemental-wage (bonus) calculation: the federal flat-rate withholding,
/// FICA, and state supplemental withholding on the payment, plus the resulting take-home.
/// </summary>
public sealed class BonusResult
{
    /// <summary>The supplemental payment the result was computed for.</summary>
    public decimal BonusAmount { get; init; }

    /// <summary>The tax year this result was calculated under (from the originating input's <c>TaxYear</c>).</summary>
    public int TaxYear { get; init; } = TaxYearSupport.Default;

    public UsState State { get; init; }

    /// <summary>Federal income tax withheld using the flat supplemental method (22%; 37% over $1M).</summary>
    public decimal FederalWithholding { get; init; }

    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    /// <summary>
    /// State income tax withheld on the bonus. Zero when the state has no income tax or uses
    /// the regular method (see <see cref="StateUsesRegularMethod"/>).
    /// </summary>
    public decimal StateWithholding { get; init; }

    /// <summary>The method the work state uses for supplemental wages.</summary>
    public StateSupplementalMethod StateMethod { get; init; }

    /// <summary>
    /// True when the state publishes no flat supplemental rate, so <see cref="StateWithholding"/>
    /// is 0 and the displayed net is before state income tax. The UI should surface a caveat.
    /// </summary>
    public bool StateUsesRegularMethod { get; init; }

    /// <summary>Human-readable summary of how the state amount was derived (or why it is 0).</summary>
    public string StateWithholdingDescription { get; init; } = "";

    public decimal TotalTaxes => FederalWithholding
                               + SocialSecurityWithholding + MedicareWithholding + AdditionalMedicareWithholding
                               + StateWithholding;

    /// <summary>Take-home portion of the bonus after withholding.</summary>
    public decimal NetBonus { get; init; }

    /// <summary>The tax year whose tables were used to produce this result.</summary>
    public int TaxYear { get; init; }

    /// <summary>"Show Your Work" breakdown — one line per visible row, mirroring <see cref="PaycheckResult"/>.</summary>
    public PaycheckExplanation Explanation { get; init; } = PaycheckExplanation.Empty;
}
