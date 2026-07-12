using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Delaware;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class DelawareWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsDelaware()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.DE, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithFourOptions()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(4, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married Filing Jointly", field.Options);
        Assert.Contains("Married Filing Separately", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    [Fact]
    public void Schema_ContainsAllowances()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Equal("DE W-4 Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married Filing Jointly")]
    [InlineData("Married Filing Separately")]
    [InlineData("Head of Household")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Invalid" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    // ── Single filing status ────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_NoAllowances()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 5000 * 26 = 130,000
        // std ded single = 3,250
        // taxable = 126,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-126750 @ 6.6% = 4405.50
        // total = 7349.00
        // per period = 7349.00 / 26 = 282.6538..., rounds to 282.65
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(282.65m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_WithAllowances()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 4000 * 12 = 48,000
        // std ded single = 3,250
        // taxable = 44,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-44750 @ 5.55% = 1096.125
        // total = 2097.125
        // credit = 2 * 110 = 220
        // net tax = 1877.125
        // per period = 1877.125 / 12 = 156.427..., rounds to 156.43
        Assert.Equal(156.43m, result.Withholding);
    }

    // ── Married Filing Jointly ──────────────────────────────────────

    [Fact]
    public void MarriedJoint_Biweekly_WithAllowances()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Allowances"] = 3,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 6000 * 26 = 156,000
        // std ded married joint = 6,500
        // taxable = 149,500
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-149500 @ 6.6% = 5907.00
        // total = 8850.50
        // credit = 3 * 110 = 330
        // net tax = 8520.50
        // per period = 8520.50 / 26 = 327.711538..., rounds to 327.71
        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(327.71m, result.Withholding);
    }

    // ── Married Filing Separately ───────────────────────────────────

    [Fact]
    public void MarriedSeparate_UsesSmallStandardDeduction()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Separately",
            ["Allowances"] = 1,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 5000 * 12 = 60,000
        // std ded = 3,250 (same as single for MFS)
        // taxable = 56,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-56750 @ 5.55% = 1762.125
        // total = 2763.125
        // credit = 1 * 110 = 110
        // net tax = 2653.125
        // per period = 2653.125 / 12 = 221.09375, rounds to 221.09
        Assert.Equal(221.09m, result.Withholding);
    }

    // ── Head of Household ───────────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_UsesSmallStandardDeduction()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 3000 * 26 = 78,000
        // std ded = 3,250 (same as single for HOH)
        // taxable = 74,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-74750 @ 6.6% = 973.50
        // total = 3917.00
        // credit = 2 * 110 = 220
        // net tax = 3697.00
        // per period = 3697.00 / 26 = 142.192307..., rounds to 142.19
        Assert.Equal(142.19m, result.Withholding);
    }

    // ── High-income bracket (6.6%) ──────────────────────────────────

    [Fact]
    public void HighIncome_TopBracketAt6Point6Percent()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 15000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 15000 * 12 = 180,000
        // std ded single = 3,250
        // taxable = 176,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-176750 @ 6.6% = 7705.50
        // total = 10,649.00
        // per period = 10649.00 / 12 = 887.4166..., rounds to 887.42
        Assert.Equal(887.42m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 5000 - 2000 = 3000
        // annual = 3000 * 26 = 78,000
        // std ded = 3,250
        // taxable = 74,750
        // total bracket tax = 3917.00
        // per period = 3917.00 / 26 = 150.653846..., rounds to 150.65
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(150.65m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAdded()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // base = 282.65 + 25 = 307.65
        Assert.Equal(307.65m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void LowIncome_BelowStandardDeduction_ZeroTax()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        // annual = 100 * 26 = 2,600 which is below std ded of 3,250
        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 100m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(100m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void LargeAllowances_CreditExceedsTax_FloorAtZero()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 20,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 3000 * 12 = 36,000
        // std ded = 3,250
        // taxable = 32,750
        // tax before credit is modest; credit = 20 * 110 = 2200 far exceeds tax
        // net tax floored at 0
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Pay frequency tests ─────────────────────────────────────────

    [Fact]
    public void Weekly_PayFrequency()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 2000 * 52 = 104,000
        // std ded = 3,250
        // taxable = 100,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-100750 @ 6.6% = 2689.50
        // total = 5633.00
        // per period = 5633.00 / 52 = 108.326923..., rounds to 108.33
        Assert.Equal(108.33m, result.Withholding);
    }

    [Fact]
    public void Annual_PayFrequency()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 80000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 1,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 80,000 (1 period)
        // std ded = 3,250
        // taxable = 76,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-76750 @ 6.6% = 1105.50
        // total = 4049.00
        // credit = 1 * 110 = 110
        // net tax = 3939.00
        // per period = 3939.00 / 1 = 3939.00
        Assert.Equal(3939.00m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_AllInputs()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        // Semimonthly employee: $5000 gross, $800 pre-tax, MFJ, 2 allowances, $10 extra
        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 5000 - 800 = 4200
        // annual = 4200 * 24 = 100,800
        // std ded MFJ = 6,500
        // taxable = 94,300
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // 60000-94300 @ 6.6% = 2263.80
        // total = 5207.30
        // credit = 2 * 110 = 220
        // net tax = 4987.30
        // per period = 4987.30 / 24 = 207.804166..., rounds to 207.80
        // + extra = 207.80 + 10 = 217.80
        Assert.Equal(4200m, result.TaxableWages);
        Assert.Equal(217.80m, result.Withholding);
    }

    // ── No disability insurance for Delaware ─────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Bracket boundary test ───────────────────────────────────────

    [Fact]
    public void IncomeAtExactBracketBoundary_60000()
    {
        var calc = new DelawareWithholdingCalculator(TestSchemas.Provider);

        // Engineer taxable income to be exactly $60,000 (bracket boundary)
        // Need annual wage - std ded = 60,000 → annual = 63,250
        // Monthly: 63,250 / 12 = 5270.833... — use Annual pay to be exact
        var context = new CommonWithholdingContext(
            UsState.DE,
            GrossWages: 63_250m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 63,250
        // std ded = 3,250
        // taxable = 60,000 (exactly at top of 5.55% bracket)
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-60000 @ 5.55% = 1942.50
        // total = 2943.50
        Assert.Equal(2943.50m, result.Withholding);
    }
}
