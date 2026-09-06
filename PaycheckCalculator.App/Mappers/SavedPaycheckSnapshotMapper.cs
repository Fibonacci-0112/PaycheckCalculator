using PaycheckCalculator.App.Helpers;
using PaycheckCalculator.App.Models;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Shared.Snapshots;

namespace PaycheckCalculator.App.Mappers;

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
                StateTaxLines = card.StateTaxLines.Select(line => new SavedStateTaxLineDto
                {
                    Kind = line.Kind,
                    Label = line.Label,
                    Amount = line.Amount,
                    ShortCode = line.ShortCode
                }).ToList(),
                PreTaxDeductions = card.PreTaxDeductions,
                PostTaxDeductions = card.PostTaxDeductions,
                TotalTaxes = card.TotalTaxes,
                NetPay = card.NetPay,
                IsGrossUp = card.IsGrossUp,
                TargetNetPay = card.TargetNetPay,
                GrossUpCost = card.GrossUpCost,
                TaxYear = card.TaxYear
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
            StateTaxLines = r.StateTaxLines.Select(line => new StateTaxLine
            {
                Kind = line.Kind,
                Label = line.Label,
                Amount = line.Amount,
                ShortCode = line.ShortCode
            }).ToList(),
            PreTaxDeductions = r.PreTaxDeductions,
            PostTaxDeductions = r.PostTaxDeductions,
            TotalTaxes = r.TotalTaxes,
            NetPay = r.NetPay,
            IsGrossUp = r.IsGrossUp,
            TargetNetPay = r.TargetNetPay,
            GrossUpCost = r.GrossUpCost,
            TaxYear = r.TaxYear,
            StateName = EnumDisplay.UsStateName(dto.Input.State.ToString())
        };
    }
}
