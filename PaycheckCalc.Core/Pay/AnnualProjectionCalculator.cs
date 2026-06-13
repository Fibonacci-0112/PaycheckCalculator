using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;

namespace PaycheckCalc.Core.Pay;

/// <summary>
/// Computes annual projections from a single per-period paycheck result.
/// Produces annualized totals, projected year-to-date amounts by paycheck number,
/// remaining paychecks this year, and an estimated over/under withholding figure.
/// </summary>
public sealed class AnnualProjectionCalculator
{
    private readonly Irs15TPercentageCalculator _fed;
    private readonly FicaCalculator _fica;

    public AnnualProjectionCalculator(Irs15TPercentageCalculator fed, FicaCalculator fica)
    {
        _fed = fed;
        _fica = fica;
    }

    /// <summary>
    /// Computes annual projections from a per-period result and input.
    /// </summary>
    /// <param name="input">The original paycheck input (for W-4 data and frequency).</param>
    /// <param name="result">The computed per-period paycheck result.</param>
    public AnnualProjection Calculate(PaycheckInput input, PaycheckResult result)
    {
        int periods = PayPeriodsPerYear(input.Frequency);
        int paycheckNum = Math.Clamp(input.PaycheckNumber, 1, periods);
        int remaining = periods - paycheckNum;

        // ── Annualized amounts (per-period × periods/year) ──────
        decimal annualGross = R(result.GrossPay * periods);
        decimal annualPreTaxDeductions = R(result.PreTaxDeductions * periods);
        decimal annualPostTaxDeductions = R(result.PostTaxDeductions * periods);
        decimal annualFedTaxable = R(result.FederalTaxableIncome * periods);
        decimal annualFicaTaxable = R(result.FicaTaxableWages * periods);
        decimal annualStateTaxable = R(result.StateTaxableWages * periods);
        decimal annualFedWithholding = R(result.FederalWithholding * periods);
        decimal annualStateWithholding = R(result.StateWithholding * periods);
        decimal annualFica = R((result.SocialSecurityWithholding + result.MedicareWithholding + result.AdditionalMedicareWithholding) * periods);
        decimal annualNet = R(result.NetPay * periods);

        // ── Projected YTD (per-period × current paycheck number) ─
        decimal ytdGross = R(result.GrossPay * paycheckNum);
        decimal ytdFedWithholding = R(result.FederalWithholding * paycheckNum);
        decimal ytdStateWithholding = R(result.StateWithholding * paycheckNum);
        decimal ytdFica = R((result.SocialSecurityWithholding + result.MedicareWithholding + result.AdditionalMedicareWithholding) * paycheckNum);
        decimal ytdNet = R(result.NetPay * paycheckNum);

        // ── Estimated annual tax liabilities ────────────────────
        // Federal: re-run the IRS 15-T calculator with Annual frequency
        // and Step4c zeroed out so we get the "base" liability without
        // voluntary extra withholding.
        var liabilityW4 = new FederalW4Input
        {
            FilingStatus = input.FederalW4.FilingStatus,
            Step2Checked = input.FederalW4.Step2Checked,
            Step3TaxCredits = input.FederalW4.Step3TaxCredits,
            Step4aOtherIncome = input.FederalW4.Step4aOtherIncome,
            Step4bDeductions = input.FederalW4.Step4bDeductions,
            Step4cExtraWithholding = 0m  // exclude voluntary extra
        };
        decimal estimatedFedLiability = _fed.CalculateWithholding(
            annualFedTaxable, PayFrequency.Annual, liabilityW4);

        // FICA: compute directly against annualized wages with wage-base caps.
        decimal estimatedFicaLiability = EstimateFicaLiability(annualFicaTaxable);

        // State: use annualized state withholding as the best available estimate
        // since state calculators also use annualized percentage methods internally.
        decimal estimatedStateLiability = annualStateWithholding;

        decimal annualTotalWithholding = R(annualFedWithholding + annualStateWithholding + annualFica);
        decimal estimatedTotal = R(estimatedFedLiability + estimatedStateLiability + estimatedFicaLiability);
        decimal overUnder = R(annualTotalWithholding - estimatedTotal);

        return new AnnualProjection
        {
            PayPeriodsPerYear = periods,
            CurrentPaycheckNumber = paycheckNum,
            RemainingPaychecks = remaining,

            AnnualizedGrossPay = annualGross,
            AnnualizedPreTaxDeductions = annualPreTaxDeductions,
            AnnualizedPostTaxDeductions = annualPostTaxDeductions,
            AnnualizedFederalTaxableWages = annualFedTaxable,
            AnnualizedFicaTaxableWages = annualFicaTaxable,
            AnnualizedStateTaxableWages = annualStateTaxable,
            AnnualizedFederalWithholding = annualFedWithholding,
            AnnualizedStateWithholding = annualStateWithholding,
            AnnualizedFica = annualFica,
            AnnualizedNetPay = annualNet,

            ProjectedYtdGrossPay = ytdGross,
            ProjectedYtdFederalWithholding = ytdFedWithholding,
            ProjectedYtdStateWithholding = ytdStateWithholding,
            ProjectedYtdFica = ytdFica,
            ProjectedYtdNetPay = ytdNet,

            EstimatedAnnualFederalLiability = estimatedFedLiability,
            EstimatedAnnualFicaLiability = estimatedFicaLiability,
            AnnualizedTotalWithholding = annualTotalWithholding,
            EstimatedTotalLiability = estimatedTotal,
            OverUnderWithholding = overUnder
        };
    }

    /// <summary>
    /// Estimates annual FICA liability using wage-base caps and thresholds.
    /// </summary>
    private decimal EstimateFicaLiability(decimal annualFicaWages)
    {
        decimal ss = Math.Min(annualFicaWages, _fica.SocialSecurityWageBase) * FicaCalculator.SocialSecurityRate;
        decimal medicare = annualFicaWages * FicaCalculator.MedicareRate;
        decimal addlMedicare = Math.Max(0m, annualFicaWages - _fica.AdditionalMedicareEmployerThreshold) * FicaCalculator.AdditionalMedicareRate;
        return R(ss + medicare + addlMedicare);
    }

    private static int PayPeriodsPerYear(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        PayFrequency.Daily => 260,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
