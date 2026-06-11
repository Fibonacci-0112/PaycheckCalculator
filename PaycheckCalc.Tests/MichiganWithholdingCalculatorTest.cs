using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Michigan;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class MichiganWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMichigan()
    {
        var calc = new MichiganWithholdingCalculator();
        Assert.Equal(UsState.MI, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsExemptions()
    {
        var calc = new MichiganWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Equal("MI-W4 Exemptions", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new MichiganWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    [Fact]
    public void Schema_DoesNotExposeFilingStatus()
    {
        // Michigan's 2026 Form 446 percentage-method formula does not
        // distinguish between filing statuses — exemptions are the only
        // lever that varies between employees.
        var calc = new MichiganWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.DoesNotContain(schema, f => f.Key == "FilingStatus");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_PositiveExemptions_ReturnsEmpty()
    {
        var calc = new MichiganWithholdingCalculator();
        var values = new StateInputValues { ["Exemptions"] = 3 };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new MichiganWithholdingCalculator();
        var values = new StateInputValues { ["Exemptions"] = -1 };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Exemptions", StringComparison.OrdinalIgnoreCase));
    }

    // ── 2026 Form 446 worked examples ───────────────────────────────
    // Michigan Form 446, "2026 Michigan Income Tax Withholding Guide",
    // Percentage Formula Method: rate 4.25%, $5,900 per MI-W4 exemption.

    [Fact]
    public void Form446_WeeklyExample_900Wages_OneExemption_Is33_43()
    {
        // 2026 Michigan Form 446 Percentage Formula Method worked example:
        // weekly gross $900, 1 MI-W4 exemption.
        //   Weekly exemption = 5900 / 52 = 113.4615384...
        //   Taxable         = 900 − 113.4615384... = 786.5384615...
        //   Withholding     = 786.5384615... × 0.0425 = 33.4278846...
        //                    → rounds to $33.43.
        // (This calculator uses exact arithmetic on the annual exemption
        // rather than rounding the per-period exemption to cents first,
        // which matches the mathematical form of the Form 446 formula.)
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 900m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues { ["Exemptions"] = 1 };

        var result = calc.Calculate(context, values);

        Assert.Equal(900m, result.TaxableWages);
        Assert.Equal(33.43m, result.Withholding);
    }

    [Fact]
    public void Form446_BiweeklyExample_1800Wages_OneExemption()
    {
        // Biweekly example: $1,800 gross, 1 exemption.
        // Biweekly exemption = $5,900 / 26 = 226.923076...
        // Taxable = 1800 − 226.923... = 1573.076923...
        // Withholding = 1573.076923... × 0.0425 = 66.85576... → $66.86.
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 1800m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["Exemptions"] = 1 };

        var result = calc.Calculate(context, values);

        Assert.Equal(1800m, result.TaxableWages);
        Assert.Equal(66.86m, result.Withholding);
    }

    // ── Flat rate with no exemptions ────────────────────────────────

    [Fact]
    public void Biweekly_NoExemptions_FlatRateOnFullWages()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // No exemptions: 2000 × 0.0425 = 85.00
        Assert.Equal(2000m, result.TaxableWages);
        Assert.Equal(85.00m, result.Withholding);
    }

    [Fact]
    public void Monthly_ThreeExemptions_ReducesTaxableAmount()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues { ["Exemptions"] = 3 };

        var result = calc.Calculate(context, values);

        // Annual exemption = 3 × 5900 = 17,700
        // Monthly exemption = 17,700 / 12 = 1,475.00
        // Taxable = 5000 − 1475 = 3525
        // Withholding = 3525 × 0.0425 = 149.8125 → $149.81
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(149.81m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceStateTaxableWages()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);

        var result = calc.Calculate(context, new StateInputValues());

        // State wages = 2000 − 200 = 1800, no exemptions.
        // Withholding = 1800 × 0.0425 = 76.50
        Assert.Equal(1800m, result.TaxableWages);
        Assert.Equal(76.50m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAddedAfterTaxCalc()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 20m };

        var result = calc.Calculate(context, values);

        // 2000 × 0.0425 = 85.00, plus $20 extra = $105.00
        Assert.Equal(105.00m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void LargeExemptions_FloorAtZeroTax()
    {
        var calc = new MichiganWithholdingCalculator();

        // Low-wage weekly employee claiming many exemptions.
        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 100m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues { ["Exemptions"] = 10 };

        var result = calc.Calculate(context, values);

        // Weekly exemption = 10 × 5900 / 52 = 1134.61...; taxable floored at 0.
        Assert.Equal(100m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_ExemptionsPreTaxAndExtraWithholding()
    {
        var calc = new MichiganWithholdingCalculator();

        // Semimonthly employee: $3,000 gross, $250 pre-tax, 2 exemptions,
        // $10 extra per MI-W4 Line 6.
        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 250m);
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        // State wages = 3000 − 250 = 2750
        // Semimonthly exemption = 2 × 5900 / 24 = 491.666...
        // Taxable = 2750 − 491.666... = 2258.333...
        // Withholding = 2258.333... × 0.0425 = 95.979166... → $95.98
        // Total = 95.98 + 10 = $105.98
        Assert.Equal(2750m, result.TaxableWages);
        Assert.Equal(105.98m, result.Withholding);
    }

    // ── No disability insurance for Michigan ────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_ExemptionSteps_ShowAnnualAllowanceAndFlatRate()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["Exemptions"] = 2 };

        var result = calc.Calculate(context, values);

        Assert.NotNull(result.WithholdingSteps);
        // Annual exemption = 2 × 5900 = 11,800
        var annualStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual exemption allowance (MI-W4)");
        Assert.Equal(11_800m, annualStep.Value);
        // Per-period = 11800 / 26 = 453.846...; taxable = 3546.153...;
        // withholding = 3546.153... × 4.25% = 150.71
        Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Per-period exemption"));
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("4.25%"));
        Assert.Equal(150.71m, rateStep.Value);
        Assert.Equal(150.71m, result.Withholding);
        Assert.Contains("Form 446", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_NoExemptions_OmitsExemptionSteps()
    {
        var calc = new MichiganWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.MI,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.DoesNotContain(result.WithholdingSteps!, s => s.Label == "Annual exemption allowance (MI-W4)");
        // 2000 × 4.25% = 85.00 on the full taxable wages
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("4.25%"));
        Assert.Equal(85.00m, rateStep.Value);
    }
}
