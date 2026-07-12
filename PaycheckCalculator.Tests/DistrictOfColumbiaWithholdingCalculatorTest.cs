using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.DistrictOfColumbia;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class DistrictOfColumbiaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsDC()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.DC, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithFourOptions()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("D-4 Filing Status", field.Label);
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
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Equal("D-4 Withholding Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding", field.Label);
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
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Invalid" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = -1
        };
        var errors = calc.Validate(values);
        Assert.Contains(errors, e => e.Contains("Allowances"));
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        };
        var errors = calc.Validate(values);
        Assert.Contains(errors, e => e.Contains("Additional Withholding"));
    }

    // ── Single filing status ────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_NoAllowances_MidBrackets()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 4000 * 26 = 104,000
        // std ded single = 15,000
        // taxable = 89,000
        // Brackets:
        //   0-10,000     @ 4%     =   400.00
        //   10,000-40,000 @ 6%    = 1,800.00
        //   40,000-60,000 @ 6.5%  = 1,300.00
        //   60,000-89,000 @ 8.5%  = 2,465.00
        // total annual tax = 5,965.00
        // per period = 5,965 / 26 = 229.4230769..., rounds to 229.42
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(229.42m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_TopBracketsPartial()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 100_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 100,000
        // std ded single = 15,000
        // taxable = 85,000
        // Brackets:
        //   0-10,000      @ 4%    =   400.00
        //   10,000-40,000 @ 6%    = 1,800.00
        //   40,000-60,000 @ 6.5%  = 1,300.00
        //   60,000-85,000 @ 8.5%  = 2,125.00
        // total = 5,625.00
        Assert.Equal(5_625.00m, result.Withholding);
    }

    // ── Married Filing Jointly ──────────────────────────────────────

    [Fact]
    public void MarriedJoint_Monthly_WithAllowances()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 6000 * 12 = 72,000
        // std ded MFJ = 30,000
        // allowances = 2 * 1,675 = 3,350
        // taxable = 72,000 - 30,000 - 3,350 = 38,650
        // Brackets:
        //   0-10,000       @ 4% =   400.00
        //   10,000-38,650  @ 6% = 1,719.00
        // total = 2,119.00
        // per period = 2,119 / 12 = 176.5833..., rounds to 176.58
        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(176.58m, result.Withholding);
    }

    // ── Married Filing Separately (uses single-style std ded) ───────

    [Fact]
    public void MarriedSeparate_Biweekly_UsesSingleStandardDeduction()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Separately",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 3000 * 26 = 78,000
        // std ded MFS = 15,000 (same as Single)
        // taxable = 63,000
        // Brackets:
        //   0-10,000      @ 4%    =   400.00
        //   10,000-40,000 @ 6%    = 1,800.00
        //   40,000-60,000 @ 6.5%  = 1,300.00
        //   60,000-63,000 @ 8.5%  =   255.00
        // total = 3,755.00
        // per period = 3,755 / 26 = 144.4230..., rounds to 144.42
        Assert.Equal(144.42m, result.Withholding);
    }

    // ── Head of Household (uses single-style std ded) ───────────────

    [Fact]
    public void HeadOfHousehold_Semimonthly_AppliesExtraWithholding()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // annual = 3000 * 24 = 72,000
        // std ded HoH = 15,000 (same as Single)
        // taxable = 57,000
        // Brackets:
        //   0-10,000      @ 4%    =   400.00
        //   10,000-40,000 @ 6%    = 1,800.00
        //   40,000-57,000 @ 6.5%  = 1,105.00
        // total = 3,305.00
        // per period = 3,305 / 24 = 137.7083..., rounds to 137.71
        // + $25 extra = 162.71
        Assert.Equal(162.71m, result.Withholding);
    }

    // ── Pre-tax deductions reduce taxable wages ─────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesBeforeAnnualization()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // taxable per period = 5,000 - 500 = 4,500
        // annual = 4,500 * 26 = 117,000
        // std ded = 15,000; taxable = 102,000
        // Brackets:
        //   0-10,000       @ 4%    =    400.00
        //   10,000-40,000  @ 6%    =  1,800.00
        //   40,000-60,000  @ 6.5%  =  1,300.00
        //   60,000-102,000 @ 8.5%  =  3,570.00
        // total = 7,070.00
        // per period = 7,070 / 26 = 271.9230..., rounds to 271.92
        Assert.Equal(4500m, result.TaxableWages);
        Assert.Equal(271.92m, result.Withholding);
    }

    // ── Allowance exemption and clamping ────────────────────────────

    [Fact]
    public void Allowances_ReduceAnnualTaxableWages()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 3,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 4000 * 26 = 104,000
        // std ded single = 15,000
        // allowances = 3 * 1,675 = 5,025
        // taxable = 104,000 - 15,000 - 5,025 = 83,975
        // Brackets:
        //   0-10,000      @ 4%    =    400.00
        //   10,000-40,000 @ 6%    =  1,800.00
        //   40,000-60,000 @ 6.5%  =  1,300.00
        //   60,000-83,975 @ 8.5%  =  2,037.875
        // total = 5,537.875
        // per period = 5,537.875 / 26 = 212.9951..., rounds to 213.00
        Assert.Equal(213.00m, result.Withholding);
    }

    [Fact]
    public void LowWage_WithLargeAllowances_ClampsToZero()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 400m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 10,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 400 * 52 = 20,800
        // std ded + 10 * 1,675 = 15,000 + 16,750 = 31,750
        // taxable floor at 0; tax = 0
        Assert.Equal(0m, result.Withholding);
    }

    // ── Top-bracket coverage ────────────────────────────────────────

    [Fact]
    public void Single_VeryHighIncome_AppliesTopBracket()
    {
        var calc = new DistrictOfColumbiaWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.DC,
            GrossWages: 1_200_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 1,200,000; std ded = 15,000; taxable = 1,185,000
        // Brackets:
        //   0-10,000          @ 4%     =        400.00
        //   10,000-40,000     @ 6%     =      1,800.00
        //   40,000-60,000     @ 6.5%   =      1,300.00
        //   60,000-250,000    @ 8.5%   =     16,150.00
        //   250,000-500,000   @ 9.25%  =     23,125.00
        //   500,000-1,000,000 @ 9.75%  =     48,750.00
        //   1,000,000-1,185,000 @ 10.75% =   19,887.50
        // total = 111,412.50
        Assert.Equal(111_412.50m, result.Withholding);
    }
}
