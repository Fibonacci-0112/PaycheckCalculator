using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class IllinoisWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsIllinois()
    {
        var calc = new IllinoisWithholdingCalculator();
        Assert.Equal(UsState.IL, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsBasicAllowances()
    {
        var calc = new IllinoisWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "BasicAllowances");
        Assert.Equal("Basic Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalAllowances()
    {
        var calc = new IllinoisWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalAllowances");
        Assert.Equal("Additional Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new IllinoisWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_AlwaysReturnsEmpty()
    {
        var calc = new IllinoisWithholdingCalculator();
        var errors = calc.Validate(new StateInputValues());
        Assert.Empty(errors);
    }

    // ── Flat 4.95% with allowances ──────────────────────────────────

    [Fact]
    public void Biweekly_NoAllowances_FlatRateOnFullWages()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // No allowances, so taxable = 5000
        // 5000 * 0.0495 = 247.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(247.50m, result.Withholding);
    }

    [Fact]
    public void Biweekly_BasicAllowances_ReduceTaxableAmount()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 2,
            ["AdditionalAllowances"] = 0
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = 2 * 2925 = 5850
        // Per-period exemption = 5850 / 26 = 225.00
        // Taxable = 4000 - 225 = 3775
        // Withholding = 3775 * 0.0495 = 186.8625, rounds to 186.86
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(186.86m, result.Withholding);
    }

    [Fact]
    public void Biweekly_AdditionalAllowances_ReduceTaxableAmount()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 0,
            ["AdditionalAllowances"] = 3
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = 3 * 1000 = 3000
        // Per-period exemption = 3000 / 26 = 115.384615...
        // Taxable = 4000 - 115.384615... = 3884.615384...
        // Withholding = 3884.615384... * 0.0495 = 192.2884615..., rounds to 192.29
        Assert.Equal(192.29m, result.Withholding);
    }

    [Fact]
    public void Monthly_BothAllowanceTypes_CombinedExemption()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 1,
            ["AdditionalAllowances"] = 2
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = (1 * 2925) + (2 * 1000) = 4925
        // Per-period exemption = 4925 / 12 = 410.416666...
        // Taxable = 6000 - 410.416666... = 5589.583333...
        // Withholding = 5589.583333... * 0.0495 = 276.684375, rounds to 276.68
        Assert.Equal(276.68m, result.Withholding);
    }

    [Fact]
    public void Weekly_LargeAllowances_FloorAtZeroTax()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 10,
            ["AdditionalAllowances"] = 5
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = (10 * 2925) + (5 * 1000) = 34,250
        // Per-period exemption = 34250 / 52 = 658.653846...
        // Taxable = max(0, 500 - 658.65...) = 0
        // Withholding = 0
        Assert.Equal(500m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // Taxable wages = 5000 - 1000 = 4000
        // No allowances, so withholding = 4000 * 0.0495 = 198.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(198.00m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAdded()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // 5000 * 0.0495 + 25 = 272.50
        Assert.Equal(272.50m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
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
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
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
    public void CombinedScenario_AllowancesAndDeductionsAndExtraWithholding()
    {
        var calc = new IllinoisWithholdingCalculator();

        // Semimonthly employee: $4500 gross, $500 pre-tax, 2 basic + 1 additional, $15 extra
        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 4500m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 2,
            ["AdditionalAllowances"] = 1,
            ["AdditionalWithholding"] = 15m
        };

        var result = calc.Calculate(context, values);

        // Taxable wages = 4500 - 500 = 4000
        // Annual exemption = (2 * 2925) + (1 * 1000) = 6850
        // Per-period exemption = 6850 / 24 = 285.416666...
        // Taxable amount = 4000 - 285.416666... = 3714.583333...
        // Withholding = 3714.583333... * 0.0495 = 183.871875, rounds to 183.87
        // Total = 183.87 + 15 = 198.87
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(198.87m, result.Withholding);
    }

    [Fact]
    public void Annual_PayFrequency_FullExemptionApplied()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 80000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 1,
            ["AdditionalAllowances"] = 0
        };

        var result = calc.Calculate(context, values);

        // Annual exemption = 1 * 2925 = 2925
        // Per-period exemption = 2925 / 1 = 2925 (annual = 1 period)
        // Taxable = 80000 - 2925 = 77075
        // Withholding = 77075 * 0.0495 = 3815.2125, rounds to 3815.21
        Assert.Equal(80000m, result.TaxableWages);
        Assert.Equal(3815.21m, result.Withholding);
    }

    // ── No disability insurance for Illinois ────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_AllowanceSteps_ShowAnnualAndPerPeriodExemption()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["BasicAllowances"] = 2,
            ["AdditionalAllowances"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.NotNull(result.WithholdingSteps);
        // Annual exemption = 2 × 2925 = 5850; per-period = 5850 / 26 = 225.00
        var annualStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual exemption from IL-W-4 allowances");
        Assert.Equal(5850m, annualStep.Value);
        var perPeriodStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Per-period exemption"));
        Assert.Equal(225m, perPeriodStep.Value);
        // Taxable amount = 4000 − 225 = 3775; withholding = 3775 × 4.95% = 186.86
        var afterStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Wages subject to tax after exemption");
        Assert.Equal(3775m, afterStep.Value);
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("4.95%"));
        Assert.Equal(186.86m, rateStep.Value);
        Assert.Contains("IL-700-T", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_NoAllowances_OmitsExemptionSteps()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.DoesNotContain(result.WithholdingSteps!, s => s.Label == "Annual exemption from IL-W-4 allowances");
        // 5000 × 4.95% = 247.50, computed directly on full taxable wages
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("4.95%"));
        Assert.Equal(247.50m, rateStep.Value);
    }

    [Fact]
    public void Explanation_ExtraWithholding_AppendsStepWithTotal()
    {
        var calc = new IllinoisWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.IL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        // 247.50 + 25 = 272.50 shown on the trailing extra-withholding step
        var extraStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Extra withholding");
        Assert.Equal(272.50m, extraStep.Value);
        Assert.Equal(272.50m, result.Withholding);
    }
}
