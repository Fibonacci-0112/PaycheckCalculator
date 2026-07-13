using PaycheckCalculator.App.ViewModels;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.App.Mappers;

/// <summary>
/// Maps <see cref="CalculatorViewModel"/> state to a domain
/// <see cref="PaycheckInput"/> ready for the calculation engine.
/// </summary>
public static class PaycheckInputMapper
{
    public static PaycheckInput Map(CalculatorViewModel vm, StateInputValues stateValues)
    {
        return new PaycheckInput
        {
            Frequency = vm.Frequency,
            PayType = vm.PayType,
            HourlyRate = vm.HourlyRate,
            RegularHours = vm.RegularHours,
            OvertimeHours = vm.OvertimeHours,
            OvertimeMultiplier = vm.OvertimeMultiplier,
            SalaryAmount = vm.SalaryAmount,
            SalaryBasis = vm.SalaryBasis,
            State = vm.SelectedState,
            StateInputValues = stateValues,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = vm.FederalFilingStatus,
                Step2Checked = vm.FederalStep2Checked,
                Step3TaxCredits = vm.FederalStep3Credits,
                Step4aOtherIncome = vm.FederalStep4aOtherIncome,
                Step4bDeductions = vm.FederalStep4bDeductions,
                Step4cExtraWithholding = vm.FederalStep4cExtraWithholding
            },
            Deductions = vm.Deductions.Select(d => d.ToDeduction()).ToArray(),
            PaycheckNumber = vm.PaycheckNumber,
            YtdSocialSecurityWages = vm.YtdSocialSecurityWages,
            YtdMedicareWages = vm.YtdMedicareWages
        };
    }
}
