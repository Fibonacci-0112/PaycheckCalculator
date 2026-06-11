using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;
using Xunit;

/// <summary>
/// Tennessee has no individual income tax on wages (Hall income tax repealed 2021).
/// Withholding is always zero; the adapter returns a descriptive result with no schema fields.
/// </summary>
public class TennesseeWithholdingCalculatorTest
{
    private static NoIncomeTaxWithholdingAdapter CreateCalc() =>
        new(UsState.TN);

    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsTennessee()
    {
        var calc = CreateCalc();
        Assert.Equal(UsState.TN, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_IsEmpty()
    {
        // Tennessee has no income tax, so no state-specific input fields are needed.
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
        // Tennessee levies no income tax on wages, so withholding is always $0.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.TN,
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
            UsState.TN,
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
            UsState.TN,
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
        // Pre-tax deductions have no effect because there is no TN income tax to begin with.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.TN,
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
            UsState.TN,
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
            UsState.TN,
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
            UsState.TN,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var step = Assert.Single(result.WithholdingSteps!);
        Assert.Equal("No state income tax", step.Label);
        Assert.Equal(0m, step.Value);
        Assert.Contains("TN", result.WithholdingReference);
    }
}
