namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Inputs for a one-off supplemental-wage (bonus) calculation using the federal flat-rate
/// method. Unlike <see cref="PaycheckInput"/>, a bonus is not annualized or run through the
/// W-4 percentage tables — it is withheld at the flat supplemental rates — so only the
/// fields that affect that computation are present.
/// </summary>
public sealed class BonusInput
{
    /// <summary>The supplemental payment (bonus, commission, award, etc.) for this run.</summary>
    public decimal BonusAmount { get; init; }

    /// <summary>The work state, used to look up the state supplemental rule.</summary>
    public UsState State { get; init; } = UsState.OK;

    /// <summary>
    /// Supplemental wages already paid to the employee earlier in the calendar year. Used to
    /// place this payment relative to the federal $1,000,000 cumulative threshold (22% below,
    /// 37% above). Defaults to 0.
    /// </summary>
    public decimal YtdSupplementalWages { get; init; }

    /// <summary>
    /// Year-to-date Social Security wages, so the FICA Social Security wage-base cap is
    /// honored on the bonus. Defaults to 0.
    /// </summary>
    public decimal YtdSocialSecurityWages { get; init; }

    /// <summary>
    /// Year-to-date Medicare wages, so the 0.9% Additional Medicare threshold ($200,000) is
    /// honored on the bonus. Defaults to 0.
    /// </summary>
    public decimal YtdMedicareWages { get; init; }
}
