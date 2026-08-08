using PaycheckCalculator.App.Helpers;
using PaycheckCalculator.App.Models;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.App.Mappers;

/// <summary>
/// Maps a domain <see cref="PaycheckResult"/> to a
/// <see cref="ResultCardModel"/> presentation model for the UI.
/// </summary>
public static class ResultCardMapper
{
    public static ResultCardModel Map(PaycheckResult result)
        => MapInternal(result, isGrossUp: false, targetNetPay: 0m);

    /// <summary>
    /// Maps a <see cref="GrossUpResult"/> for display: the per-period breakdown computed
    /// at the solved gross, annotated with the gross-up target and cost.
    /// </summary>
    public static ResultCardModel MapGrossUp(GrossUpResult result)
        => MapInternal(result.Paycheck, isGrossUp: true, targetNetPay: result.TargetNetPay);

    /// <summary>
    /// Maps a <see cref="BonusResult"/> for display. The bonus has no annualized W-4 figures,
    /// so the taxable-income rows simply reflect the full bonus (the base each tax applies to).
    /// </summary>
    public static ResultCardModel MapBonus(BonusResult result)
        => new()
        {
            GrossPay = result.BonusAmount,
            FederalTaxableIncome = result.BonusAmount,
            FicaTaxableWages = result.BonusAmount,
            StateTaxableWages = result.BonusAmount,
            FederalWithholding = result.FederalWithholding,
            SocialSecurityWithholding = result.SocialSecurityWithholding,
            MedicareWithholding = result.MedicareWithholding,
            AdditionalMedicareWithholding = result.AdditionalMedicareWithholding,
            StateWithholding = result.StateWithholding,
            StateDisabilityInsurance = 0m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            TotalTaxes = result.TotalTaxes,
            NetPay = result.NetBonus,
            StateName = EnumDisplay.UsStateName(result.State.ToString()),
            Explanation = result.Explanation,
            IsBonus = true,
            BonusStateUsesRegularMethod = result.StateUsesRegularMethod,
            BonusStateDescription = result.StateWithholdingDescription,
            AccuracyNotes = result.AccuracyNotes,
            TaxYear = result.TaxYear
        };

    /// <summary>
    /// Maps a <see cref="SelfEmploymentResult"/> for display. There is no W-4 / federal income
    /// tax withholding in this mode, so the federal-withholding slot carries the total
    /// self-employment tax and the state slot carries the estimated state income tax. The
    /// quarterly estimated-payment schedule is attached for the 1040-ES table.
    /// </summary>
    public static ResultCardModel MapSelfEmployment(SelfEmploymentResult result)
        => new()
        {
            GrossPay = result.AnnualNetEarnings,
            FederalTaxableIncome = result.NetEarningsSubjectToSeTax,
            FicaTaxableWages = result.NetEarningsSubjectToSeTax,
            StateTaxableWages = result.AnnualNetEarnings,
            FederalWithholding = result.SelfEmploymentTax,
            SocialSecurityWithholding = result.SocialSecurityTax,
            MedicareWithholding = result.MedicareTax,
            AdditionalMedicareWithholding = result.AdditionalMedicareTax,
            StateWithholding = result.StateIncomeTax,
            StateDisabilityInsurance = 0m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            TotalTaxes = result.TotalTax,
            NetPay = result.TakeHome,
            StateName = EnumDisplay.UsStateName(result.State.ToString()),
            Explanation = result.Explanation,
            IsSelfEmployment = true,
            QuarterlyEstimates = result.QuarterlyEstimates,
            AccuracyNotes = result.AccuracyNotes,
            TaxYear = result.TaxYear
        };

    private static ResultCardModel MapInternal(PaycheckResult result, bool isGrossUp, decimal targetNetPay)
    {
        return new ResultCardModel
        {
            GrossPay = result.GrossPay,
            FederalTaxableIncome = result.FederalTaxableIncome,
            FicaTaxableWages = result.FicaTaxableWages,
            StateTaxableWages = result.StateTaxableWages,
            FederalWithholding = result.FederalWithholding,
            SocialSecurityWithholding = result.SocialSecurityWithholding,
            MedicareWithholding = result.MedicareWithholding,
            AdditionalMedicareWithholding = result.AdditionalMedicareWithholding,
            StateWithholding = result.StateWithholding,
            StateDisabilityInsurance = result.StateDisabilityInsurance,
            StateDisabilityInsuranceLabel = result.StateDisabilityInsuranceLabel,
            PreTaxDeductions = result.PreTaxDeductions,
            PostTaxDeductions = result.PostTaxDeductions,
            TotalTaxes = result.TotalTaxes,
            NetPay = result.NetPay,
            StateName = EnumDisplay.UsStateName(result.State.ToString()),
            Explanation = result.Explanation,
            IsGrossUp = isGrossUp,
            TargetNetPay = targetNetPay,
            GrossUpCost = isGrossUp ? result.GrossPay - targetNetPay : 0m,
            TaxYear = result.TaxYear
        };
    }
}
