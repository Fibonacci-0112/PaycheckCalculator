using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Iowa;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class IowaWithholdingCalculatorTest
{
    [Fact]
    public void State_ReturnsIowa()
    {
        var calc = new IowaWithholdingCalculator();
        Assert.Equal(UsState.IA, calc.State);
    }

    [Fact]
    public void Schema_HasExtraWithholdingOnly()
    {
        var calc = new IowaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.Single(schema);
        Assert.Equal("AdditionalWithholding", schema[0].Key);
        Assert.Equal(StateFieldType.Decimal, schema[0].FieldType);
    }

    [Fact]
    public void Validate_AnyInput_ReturnsNoErrors()
    {
        var calc = new IowaWithholdingCalculator();
        Assert.Empty(calc.Validate(new StateInputValues()));
        Assert.Empty(calc.Validate(new StateInputValues { ["AdditionalWithholding"] = 25m }));
    }

    [Fact]
    public void FlatRate_AppliedToGrossWages_Biweekly()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // Annual = 5,000 × 26 = 130,000 × 3.65% = 4,745.00, ÷ 26 = 182.50 per period.
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(182.50m, result.Withholding);
    }

    [Fact]
    public void FlatRate_AppliedToGrossWages_Monthly()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // Annual = 6,000 × 12 = 72,000 × 3.65% = 2,628.00, ÷ 12 = 219.00 per period.
        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(219.00m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // Taxable = 5,000 - 1,000 = 4,000.  Annual 4,000 × 26 = 104,000 × 3.65%
        // = 3,796.00, ÷ 26 = 146.00 per period.
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(146.00m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAddedToBase()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 50m };

        var result = calc.Calculate(context, values);

        // Base 182.50 + 50.00 = 232.50.
        Assert.Equal(232.50m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductionsGreaterThanGross_FlooredAtZero()
    {
        var calc = new IowaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IA,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 750m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }
}
