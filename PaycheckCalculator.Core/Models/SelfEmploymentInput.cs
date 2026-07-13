using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Inputs for a self-employment / 1099-contractor calculation. The self-employment tax
/// (the combined employer + employee halves of Social Security and Medicare) is a
/// <b>federal</b> tax computed on annual net earnings — there is no per-period
/// annualization or W-4 percentage table — so only the fields that affect that
/// computation, the state income-tax estimate, and the FICA wage-base caps are present.
/// </summary>
public sealed class SelfEmploymentInput
{
    /// <summary>
    /// Annual net self-employment earnings — your Schedule C net profit (gross receipts
    /// minus business expenses). This is the "gross pay" the user enters; the
    /// self-employment tax is computed on 92.35% of it.
    /// </summary>
    public decimal AnnualNetEarnings { get; init; }

    /// <summary>The state the contractor files in, used to estimate state income tax on the earnings.</summary>
    public UsState State { get; init; } = UsState.OK;

    /// <summary>
    /// Dynamic state-specific input values populated by the UI from the state's schema
    /// (filing status, allowances, etc.), used by the state income-tax estimate.
    /// </summary>
    public StateInputValues? StateInputValues { get; init; }

    /// <summary>
    /// Wages already subject to Social Security tax earlier in the year (e.g. from a W-2
    /// job). The 12.4% Social Security portion only applies to net earnings up to the
    /// annual wage base, so prior wages reduce the remaining base. Defaults to 0.
    /// </summary>
    public decimal YtdSocialSecurityWages { get; init; }

    /// <summary>
    /// Medicare wages already earned this year (e.g. from a W-2 job), used to place these
    /// earnings relative to the $200,000 Additional Medicare threshold. Defaults to 0.
    /// </summary>
    public decimal YtdMedicareWages { get; init; }

    /// <summary>
    /// The tax year this self-employment calculation is for.  0 (default) means "use the
    /// current supported year" (<see cref="TaxYearSupport.CurrentTaxYear"/>).
    /// </summary>
    public int TaxYear { get; init; } = 0;
}
