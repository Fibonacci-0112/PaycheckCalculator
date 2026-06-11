using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.Washington;
using Xunit;

/// <summary>
/// Washington has no state individual income tax, so income-tax withholding is always
/// zero.  The WashingtonWithholdingCalculator adds a WA Cares Fund (Long-Term Care)
/// deduction at 0.58% of all gross wages.  Employees with an approved DSHS exemption
/// certificate may opt out via the WaCaresExempt field.
/// </summary>
public class WashingtonWithholdingCalculatorTest
{
    private static WashingtonWithholdingCalculator CreateCalc() => new();

    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsWashington()
    {
        var calc = CreateCalc();
        Assert.Equal(UsState.WA, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsWaCaresExemptToggle()
    {
        var calc = CreateCalc();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "WaCaresExempt");
        Assert.Equal("WA Cares Fund Exempt", field.Label);
        Assert.Equal(StateFieldType.Toggle, field.FieldType);
        Assert.False(field.IsRequired);
        Assert.Equal(false, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExactlyOneField()
    {
        // No filing-status or allowance fields: WA has no income tax.
        var calc = CreateCalc();
        Assert.Single(calc.GetInputSchema());
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_AlwaysReturnsNoErrors()
    {
        var calc = CreateCalc();
        var errors = calc.Validate(new StateInputValues());
        Assert.Empty(errors);
    }

    // ── Zero income-tax withholding ──────────────────────────────────

    [Fact]
    public void Biweekly_AnyWages_ReturnsZeroIncometaxWithholding()
    {
        // Washington levies no income tax on wages, so Withholding is always $0.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void TaxableWages_IsAlwaysZero_BecauseNoIncomeTax()
    {
        // No income tax means no "taxable wages" for income-tax purposes.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 8_000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
    }

    // ── WA Cares Fund (Long-Term Care) ───────────────────────────────

    [Fact]
    public void WaCaresFund_Biweekly_5000Gross()
    {
        // WA Cares Fund = 5,000 × 0.58% = $29.00
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(29.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void WaCaresFund_Weekly_3000Gross()
    {
        // WA Cares Fund = 3,000 × 0.58% = $17.40
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(17.40m, result.DisabilityInsurance);
    }

    [Fact]
    public void WaCaresFund_Monthly_12500Gross()
    {
        // WA Cares Fund = 12,500 × 0.58% = $72.50
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 12_500m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(72.50m, result.DisabilityInsurance);
    }

    [Fact]
    public void WaCaresFund_RoundingAwayFromZero()
    {
        // 1,725 × 0.0058 = 10.005 → rounds up to $10.01 (AwayFromZero)
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 1_725m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(10.01m, result.DisabilityInsurance);
    }

    [Fact]
    public void WaCaresFund_ZeroGross_ReturnsZero()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
        Assert.Equal(0m, result.Withholding);
    }

    // ── WA Cares Fund exempt ─────────────────────────────────────────

    [Fact]
    public void WaCaresExempt_True_SuppressesDeduction()
    {
        // Employees with an approved DSHS exemption certificate pay no WA Cares premium.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WaCaresExempt"] = true };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.DisabilityInsurance);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void WaCaresExempt_False_AppliesDeduction()
    {
        // Explicit false is the same as the default: WA Cares premium applies.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WaCaresExempt"] = false };

        var result = calc.Calculate(context, values);

        // 5,000 × 0.58% = $29.00
        Assert.Equal(29.00m, result.DisabilityInsurance);
    }

    // ── Pre-tax deductions do not reduce WA Cares base ───────────────

    [Fact]
    public void WaCaresFund_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        // The WA Cares Fund premium is assessed on ALL gross wages.
        // Pre-tax benefit deductions (e.g., 401(k), health insurance) do not
        // reduce the wage base for WA Cares purposes.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 6_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1_500m);

        var result = calc.Calculate(context, new StateInputValues());

        // WA Cares = 6,000 × 0.58% = $34.80  (gross, not 4,500)
        Assert.Equal(34.80m, result.DisabilityInsurance);
    }

    // ── Disability insurance label ───────────────────────────────────

    [Fact]
    public void DisabilityInsuranceLabel_IsWaCaresFund()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 4_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal("WA Cares Fund (Long-Term Care)", result.DisabilityInsuranceLabel);
    }

    // ── Explanation ──────────────────────────────────────────────────

    [Fact]
    public void Explanation_NoIncomeTax_SingleStep()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var step = Assert.Single(result.WithholdingSteps!);
        Assert.Equal("No state income tax", step.Label);
        Assert.Equal(0m, step.Value);
    }

    [Fact]
    public void Explanation_WaCaresSteps_ShowRateOnGrossWages()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // WA Cares = 5,000 × 0.58% = $29.00
        Assert.NotNull(result.DisabilityInsuranceSteps);
        var premiumStep = Assert.Single(result.DisabilityInsuranceSteps!, s => s.Label == "WA Cares Fund premium (0.58%)");
        Assert.Equal(29.00m, premiumStep.Value);
        Assert.Contains("RCW", result.DisabilityInsuranceReference);
    }

    [Fact]
    public void Explanation_WaCaresExempt_OmitsPremiumSteps()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WaCaresExempt"] = true };

        var result = calc.Calculate(context, values);

        Assert.Null(result.DisabilityInsuranceSteps);
    }
}
