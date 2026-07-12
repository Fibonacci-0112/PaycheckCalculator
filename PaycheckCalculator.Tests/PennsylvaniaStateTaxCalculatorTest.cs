using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Pennsylvania;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class PennsylvaniaWithholdingCalculatorTest
{
    [Fact]
    public void State_ReturnsPennsylvania()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        Assert.Equal(UsState.PA, calc.State);
    }

    [Fact]
    public void FlatRate_AppliedToGrossWages()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // 5000 * 0.0307 = 153.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(153.50m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // (5000 - 1000) * 0.0307 = 122.80
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(122.80m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        // 5000 * 0.0307 + 25 = 178.50
        Assert.Equal(178.50m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_FlatRateStep_ShowsTaxableWagesTimesRate()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var wageStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "State taxable wages");
        Assert.Equal(5000m, wageStep.Value);
        // 5000 × 3.07% = 153.50
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("3.07%"));
        Assert.Equal(153.50m, rateStep.Value);
        Assert.Contains("3.07", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_ExtraWithholding_AppendsStepWithTotal()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        // 153.50 + 25 = 178.50 shown on the trailing extra-withholding step
        var extraStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Extra withholding");
        Assert.Equal(178.50m, extraStep.Value);
    }

    [Fact]
    public void Explanation_NoExtraWithholding_OmitsStep()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.DoesNotContain(result.WithholdingSteps!, s => s.Label == "Extra withholding");
    }

    [Fact]
    public void Explanation_PreTaxDeductions_PrependGrossWageSteps()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        var grossStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Gross wages this period");
        Assert.Equal(5000m, grossStep.Value);
        var preTaxStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Less pre-tax deductions reducing state wages");
        Assert.Equal(1000m, preTaxStep.Value);
        var wageStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "State taxable wages");
        Assert.Equal(4000m, wageStep.Value);
    }
}
