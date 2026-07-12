using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Indiana;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class IndianaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsIndiana()
    {
        var calc = new IndianaWithholdingCalculator();
        Assert.Equal(UsState.IN, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsExemptions()
    {
        var calc = new IndianaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Equal("WH-4 Exemptions", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsDependentExemptions()
    {
        var calc = new IndianaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "DependentExemptions");
        Assert.Equal("Additional Dependent Exemptions", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new IndianaWithholdingCalculator();
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
        var calc = new IndianaWithholdingCalculator();
        var errors = calc.Validate(new StateInputValues());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new IndianaWithholdingCalculator();
        var values = new StateInputValues { ["Exemptions"] = -1 };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Exemptions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeDependentExemptions_ReturnsError()
    {
        var calc = new IndianaWithholdingCalculator();
        var values = new StateInputValues { ["DependentExemptions"] = -2 };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Dependent", StringComparison.OrdinalIgnoreCase));
    }

    // ── Flat 3.05% with exemptions ──────────────────────────────────

    [Fact]
    public void Biweekly_NoExemptions_FlatRateOnFullWages()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // No exemptions, so taxable = 5000
        // 5000 * 0.0305 = 152.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(152.50m, result.Withholding);
    }

    [Fact]
    public void Biweekly_PersonalExemptions_ReduceTaxableAmount()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,
            ["DependentExemptions"] = 0
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = 2 * 1000 = 2000
        // Per-period exemption = 2000 / 26 = 76.923076...
        // Taxable amount = 3000 - 76.923076... = 2923.076923...
        // Withholding = 2923.076923... * 0.0305 = 89.1538461..., rounds to 89.15
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(89.15m, result.Withholding);
    }

    [Fact]
    public void Biweekly_DependentExemptions_UseThreeThousandDollarValue()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["Exemptions"] = 0,
            ["DependentExemptions"] = 2
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = 2 * 3000 = 6000
        // Per-period exemption = 6000 / 26 = 230.769230...
        // Taxable amount = 3000 - 230.769230... = 2769.230769...
        // Withholding = 2769.230769... * 0.0305 = 84.4615384..., rounds to 84.46
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(84.46m, result.Withholding);
    }

    [Fact]
    public void Monthly_BothExemptionTypes_CombinedAnnualExemption()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,             // self + spouse
            ["DependentExemptions"] = 1     // one additional dependent
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = (2 * 1000) + (1 * 3000) = 5000
        // Per-period exemption = 5000 / 12 = 416.666666...
        // Taxable amount = 6000 - 416.666666... = 5583.333333...
        // Withholding = 5583.333333... * 0.0305 = 170.2916666..., rounds to 170.29
        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(170.29m, result.Withholding);
    }

    [Fact]
    public void Weekly_LargeExemptions_FloorAtZeroTax()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 400m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,
            ["DependentExemptions"] = 8
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = (2 * 1000) + (8 * 3000) = 26,000
        // Per-period exemption = 26000 / 52 = 500.00
        // Taxable amount = max(0, 400 - 500) = 0
        // Withholding = 0
        Assert.Equal(400m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // Taxable wages = 5000 - 1000 = 4000
        // No exemptions, so withholding = 4000 * 0.0305 = 122.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(122.00m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAdded()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // 5000 * 0.0305 + 25 = 177.50
        Assert.Equal(177.50m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
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
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_ExemptionsAndDeductionsAndExtraWithholding()
    {
        var calc = new IndianaWithholdingCalculator();

        // Semimonthly employee: $4500 gross, $500 pre-tax (e.g. 401k),
        // 2 personal exemptions + 2 dependent exemptions, $15 extra withholding.
        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 4500m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,
            ["DependentExemptions"] = 2,
            ["AdditionalWithholding"] = 15m
        };

        var result = calc.Calculate(context, values);

        // Taxable wages = 4500 - 500 = 4000
        // Annual exemption = (2 * 1000) + (2 * 3000) = 8000
        // Per-period exemption = 8000 / 24 = 333.333333...
        // Taxable amount = 4000 - 333.333333... = 3666.666666...
        // Withholding = 3666.666666... * 0.0305 = 111.8333333..., rounds to 111.83
        // Total = 111.83 + 15 = 126.83
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(126.83m, result.Withholding);
    }

    [Fact]
    public void Annual_PayFrequency_FullExemptionApplied()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 80000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["Exemptions"] = 1,
            ["DependentExemptions"] = 2
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = (1 * 1000) + (2 * 3000) = 7000
        // Per-period exemption = 7000 / 1 = 7000 (annual = 1 period)
        // Taxable = 80000 - 7000 = 73000
        // Withholding = 73000 * 0.0305 = 2226.50
        Assert.Equal(80000m, result.TaxableWages);
        Assert.Equal(2226.50m, result.Withholding);
    }

    // ── No disability insurance for Indiana ─────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new IndianaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IN,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }
}
