using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Oklahoma;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class OklahomaOw2RoundingTest
{
    private static OklahomaOw2PercentageCalculator LoadOkCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json");
        var json = File.ReadAllText(dataPath);
        return new OklahomaOw2PercentageCalculator(json);
    }

    [Fact]
    public void SemiMonthly_Married_TwoAllowances_RoundsTo37()
    {
        var ok = LoadOkCalculator();

        var allowanceTotal = ok.GetAllowanceAmount(PayFrequency.Semimonthly) * 2m;
        var okTaxable = 1825.00m - allowanceTotal;

        var wh = ok.CalculateWithholding(okTaxable, PayFrequency.Semimonthly, FilingStatus.Married);
        Assert.Equal(37m, wh);
    }

    [Fact]
    public void OklahomaWithholdingCalculator_ReturnsCorrectResult()
    {
        var inner = LoadOkCalculator();
        var calc = new OklahomaWithholdingCalculator(inner, TestSchemas.Provider);

        Assert.Equal(UsState.OK, calc.State);

        var context = new CommonWithholdingContext(
            UsState.OK,
            GrossWages: 1825.00m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(37m, result.Withholding);
        Assert.True(result.TaxableWages > 0m);
    }
}
