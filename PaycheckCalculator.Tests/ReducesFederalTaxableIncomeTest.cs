using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Oklahoma;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for the <see cref="Deduction.ReducesFederalTaxableWages"/> flag and
/// the integration scenario it enables.
/// </summary>
public sealed class ReducesFederalTaxableIncomeTest
{
    [Fact]
    public void Calculator_401kAndMedical_OklahomaSingle_MatchesExpectedTotals()
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 18m,
            RegularHours = 80m,
            State = UsState.OK,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step2Checked = true
            },
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            },
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401(k)",
                    Type = DeductionType.PreTax,
                    Amount = 5m,
                    AmountType = DeductionAmountType.Percentage,
                    ReducesFederalTaxableWages = true,
                    ReducesStateTaxableWages = true,
                    ReducesFicaWages = false
                },
                new Deduction
                {
                    Name = "Medical",
                    Type = DeductionType.PreTax,
                    Amount = 85m,
                    AmountType = DeductionAmountType.Dollar,
                    ReducesFederalTaxableWages = true,
                    ReducesStateTaxableWages = true,
                    ReducesFicaWages = true
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(1440.00m, result.GrossPay);
        Assert.Equal(157.00m, result.PreTaxDeductions);
        Assert.Equal(1283.00m, result.FederalTaxableIncome);
        Assert.Equal(1283.00m, result.StateTaxableWages);
        Assert.Equal(1355.00m, result.FicaTaxableWages);
        Assert.Equal(112.45m, result.FederalWithholding);
        Assert.Equal(84.01m, result.SocialSecurityWithholding);
        Assert.Equal(19.65m, result.MedicareWithholding);
        Assert.Equal(38.00m, result.StateWithholding);
        Assert.Equal(1028.89m, result.NetPay);
    }

    [Fact]
    public void Calculator_PreTaxDeductionWithFederalFlagOff_DoesNotReduceFederalTaxable()
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 18m,
            RegularHours = 80m,
            State = UsState.OK,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            },
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            },
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "Roth 401(k)",
                    Type = DeductionType.PreTax,
                    Amount = 100m,
                    AmountType = DeductionAmountType.Dollar,
                    ReducesFederalTaxableWages = false,
                    ReducesStateTaxableWages = false,
                    ReducesFicaWages = false
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(1440.00m, result.GrossPay);
        Assert.Equal(1440.00m, result.FederalTaxableIncome);
        Assert.Equal(1440.00m, result.StateTaxableWages);
        Assert.Equal(1440.00m, result.FicaTaxableWages);
        Assert.Equal(100.00m, result.PreTaxDeductions);
    }

    private static PayCalculator CreateCalculator()
    {
        var registry = new StateCalculatorRegistry();
        var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
