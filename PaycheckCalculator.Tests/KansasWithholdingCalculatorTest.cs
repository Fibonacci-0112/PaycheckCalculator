using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Kansas;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class KansasWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsKansas()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.KS, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("Single", field.DefaultValue);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
    }

    [Fact]
    public void Schema_ContainsAllowances()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Equal("K-4 Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultValues_ReturnsEmpty()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0
        });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MarriedStatus_ReturnsEmpty()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Married" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "HeadOfHousehold" });
        Assert.Contains(errors, e => e.Contains("Filing Status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Allowances", StringComparison.OrdinalIgnoreCase));
    }

    // ── Graduated brackets: Single ──────────────────────────────────

    [Fact]
    public void Biweekly_Single_NoAllowances_IncomeBothBrackets()
    {
        // $5,000 biweekly, Single, no allowances.
        // Annual = 5000 × 26 = 130,000 − 3,605 std = 126,395.
        // Lower bracket:  23,000 × 5.20% = 1,196.00
        // Upper bracket: 103,395 × 5.58% = 5,769.441
        // Annual tax = 6,965.441 ÷ 26 = 267.90157… → rounds to 267.90
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(267.90m, result.Withholding);
    }

    [Fact]
    public void Biweekly_Single_WagesEntirelyInLowerBracket()
    {
        // $800 biweekly, Single, no allowances.
        // Annual = 800 × 26 = 20,800 − 3,605 = 17,195 (< 23,000 threshold).
        // Tax = 17,195 × 5.20% = 894.14 ÷ 26 = 34.39384… → rounds to 34.39
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 800m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(34.39m, result.Withholding);
    }

    [Fact]
    public void Biweekly_Single_OneAllowance_ReducesTaxableIncome()
    {
        // $5,000 biweekly, Single, 1 allowance ($2,250).
        // Annual = 130,000 − 3,605 − 2,250 = 124,145.
        // Lower: 23,000 × 5.20% = 1,196.00; Upper: 101,145 × 5.58% = 5,643.891
        // Annual tax = 6,839.891 ÷ 26 = 263.07273… → rounds to 263.07
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 1
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(263.07m, result.Withholding);
    }

    // ── Graduated brackets: Married ─────────────────────────────────

    [Fact]
    public void Monthly_Married_TwoAllowances_IncomeBothBrackets()
    {
        // $6,000 monthly, Married, 2 allowances.
        // Annual = 6,000 × 12 = 72,000 − 8,240 − 4,500 = 59,260.
        // Lower: 46,000 × 5.20% = 2,392.00; Upper: 13,260 × 5.58% = 739.908
        // Annual tax = 3,131.908 ÷ 12 = 260.99233… → rounds to 260.99
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 2
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(260.99m, result.Withholding);
    }

    [Fact]
    public void Annual_Single_FullYearPayment()
    {
        // $80,000 annual, Single, no allowances.
        // Annual wages = 80,000 − 3,605 = 76,395.
        // Lower: 23,000 × 5.20% = 1,196.00; Upper: 53,395 × 5.58% = 2,979.441
        // Annual tax = 4,175.441 ÷ 1 = 4,175.441 → rounds to 4,175.44
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 80000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(80000m, result.TaxableWages);
        Assert.Equal(4175.44m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        // $4,000 semimonthly, Married, no allow, $500 pre-tax, $30 extra.
        // Taxable wages = 3,500.
        // Annual = 3,500 × 24 = 84,000 − 8,240 = 75,760.
        // Lower: 46,000 × 5.20% = 2,392; Upper: 29,760 × 5.58% = 1,660.608
        // Annual tax = 4,052.608 ÷ 24 = 168.858666… → rounds to 168.86
        // Total = 168.86 + 30 = 198.86
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["AdditionalWithholding"] = 30m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(3500m, result.TaxableWages);
        Assert.Equal(198.86m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBase()
    {
        // $5,000 biweekly, Single, no allow, $50 extra.
        // Base = 267.90 (from Biweekly_Single_NoAllowances_IncomeBothBrackets).
        // Total = 267.90 + 50 = 317.90
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = 50m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(317.90m, result.Withholding);
    }

    // ── Low income / zero withholding ───────────────────────────────

    [Fact]
    public void HighAllowances_ExceedAnnualIncome_ZeroWithholding()
    {
        // $100 weekly, Married, 10 allowances.
        // Annual = 100 × 52 = 5,200 − 8,240 = negative → 0.
        // Also subtract 10 × 2,250 = 22,500 (already zero anyway).
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 100m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 10
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(100m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Disability insurance ────────────────────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new KansasWithholdingCalculator(TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.KS,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.DisabilityInsurance);
    }
}
