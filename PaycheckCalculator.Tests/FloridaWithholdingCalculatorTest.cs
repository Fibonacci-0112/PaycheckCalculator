using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

/// <summary>
/// Florida has no state individual income tax (prohibited by Fla. Const. art. VII, § 5).
/// Withholding is always zero; the adapter returns a descriptive result with no schema fields.
/// </summary>
public class FloridaWithholdingCalculatorTest
{
    private static NoIncomeTaxWithholdingAdapter CreateCalc() =>
        new(UsState.FL);

    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsFlorida()
    {
        var calc = CreateCalc();
        Assert.Equal(UsState.FL, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_IsEmpty()
    {
        // Florida has no income tax, so no state-specific input fields are needed.
        var calc = CreateCalc();
        Assert.Empty(calc.GetInputSchema());
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_AlwaysReturnsNoErrors()
    {
        var calc = CreateCalc();
        var errors = calc.Validate(new StateInputValues());
        Assert.Empty(errors);
    }

    // ── Zero withholding ────────────────────────────────────────────

    [Fact]
    public void Biweekly_AnyWages_ReturnsZeroWithholding()
    {
        // Florida levies no income tax on wages, so withholding is always $0.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Weekly_HighWages_ReturnsZeroWithholding()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 25000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Monthly_ZeroGross_ReturnsZeroWithholding()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_DoNotAffectZeroWithholding()
    {
        // Pre-tax deductions have no effect because there is no FL income tax to begin with.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.Withholding);
    }

    // ── Description ─────────────────────────────────────────────────

    [Fact]
    public void Description_IndicatesNoStateTax()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal("No state income tax", result.Description);
    }

    // ── No disability insurance ─────────────────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_NoIncomeTax_SingleStep()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.FL,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var step = Assert.Single(result.WithholdingSteps!);
        Assert.Equal("No state income tax", step.Label);
        Assert.Equal(0m, step.Value);
        Assert.Contains("FL", result.WithholdingReference);
    }
}
