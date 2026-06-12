using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Shared.Snapshots;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Converts between the MAUI presentation model (<see cref="ResultCardModel"/> + the calculated
/// <see cref="PaycheckInput"/>) and the wire/storage <see cref="SavedPaycheckDto"/>. Restored cards have
/// no "Show Your Work" explanation (it is regenerated only for freshly calculated results), and
/// <c>StateName</c> is re-derived from the stored state.
/// </summary>
public static class SavedPaycheckSnapshotMapper
{
    public static SavedPaycheckDto ToDto(string name, PaycheckInput input, ResultCardModel card, DateTimeOffset updatedAtUtc)
        => new()
        {
            Name = name,
            UpdatedAtUtc = updatedAtUtc,
            Input = input,
            Result = new SavedPaycheckResultDto
            {
                GrossPay = card.GrossPay,
                FederalTaxableIncome = card.FederalTaxableIncome,
                FicaTaxableWages = card.FicaTaxableWages,
                StateTaxableWages = card.StateTaxableWages,
                FederalWithholding = card.FederalWithholding,
                SocialSecurityWithholding = card.SocialSecurityWithholding,
                MedicareWithholding = card.MedicareWithholding,
                AdditionalMedicareWithholding = card.AdditionalMedicareWithholding,
                StateWithholding = card.StateWithholding,
                StateDisabilityInsurance = card.StateDisabilityInsurance,
                StateDisabilityInsuranceLabel = card.StateDisabilityInsuranceLabel,
                PreTaxDeductions = card.PreTaxDeductions,
                PostTaxDeductions = card.PostTaxDeductions,
                TotalTaxes = card.TotalTaxes,
                NetPay = card.NetPay,
                IsGrossUp = card.IsGrossUp,
                TargetNetPay = card.TargetNetPay,
                GrossUpCost = card.GrossUpCost
            }
        };

    public static ResultCardModel ToResultCard(SavedPaycheckDto dto)
    {
        var r = dto.Result;
        return new ResultCardModel
        {
            GrossPay = r.GrossPay,
            FederalTaxableIncome = r.FederalTaxableIncome,
            FicaTaxableWages = r.FicaTaxableWages,
            StateTaxableWages = r.StateTaxableWages,
            FederalWithholding = r.FederalWithholding,
            SocialSecurityWithholding = r.SocialSecurityWithholding,
            MedicareWithholding = r.MedicareWithholding,
            AdditionalMedicareWithholding = r.AdditionalMedicareWithholding,
            StateWithholding = r.StateWithholding,
            StateDisabilityInsurance = r.StateDisabilityInsurance,
            StateDisabilityInsuranceLabel = r.StateDisabilityInsuranceLabel,
            PreTaxDeductions = r.PreTaxDeductions,
            PostTaxDeductions = r.PostTaxDeductions,
            TotalTaxes = r.TotalTaxes,
            NetPay = r.NetPay,
            IsGrossUp = r.IsGrossUp,
            TargetNetPay = r.TargetNetPay,
            GrossUpCost = r.GrossUpCost,
            StateName = EnumDisplay.UsStateName(dto.Input.State.ToString())
        };
    }
}
