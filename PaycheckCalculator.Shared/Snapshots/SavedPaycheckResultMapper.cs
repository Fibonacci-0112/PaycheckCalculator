using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Shared.Snapshots;

/// <summary>
/// Flattens a domain <see cref="PaycheckResult"/> (optionally annotated with gross-up details) into the
/// <see cref="SavedPaycheckResultDto"/> stored in a snapshot. Mirrors the front-ends' result mapping so a
/// saved paycheck shows the same numbers it did when calculated.
/// </summary>
public static class SavedPaycheckResultMapper
{
    public static SavedPaycheckResultDto FromResult(PaycheckResult result, bool isGrossUp = false, decimal targetNetPay = 0m)
        => new()
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
            IsGrossUp = isGrossUp,
            TargetNetPay = targetNetPay,
            GrossUpCost = isGrossUp ? result.GrossPay - targetNetPay : 0m
        };
}
