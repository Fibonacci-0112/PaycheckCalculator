using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

/// <summary>
/// New Hampshire levies no tax on earned wages, and its Interest &amp; Dividends Tax was
/// fully repealed effective January 1, 2025 — so for tax year 2026 there is no individual
/// income tax of any kind. Withholding is always zero; the adapter returns a descriptive
/// result with no schema fields.
/// </summary>
public class NewHampshireWithholdingCalculatorTest
{
    private static NoIncomeTaxWithholdingAdapter CreateCalc() =>
        new(UsState.NH);

    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNewHampshire()
    {
        var calc = CreateCalc();
        Assert.Equal(UsState.NH, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_IsEmpty()
    {
        // New Hampshire has no wage income tax, so no state-specific input fields are needed.
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
        // New Hampshire levies no income tax on wages, so withholding is always $0.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.NH,
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
            UsState.NH,
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
            UsState.NH,
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
        // Pre-tax deductions have no effect because there is no NH wage income tax to begin with.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.NH,
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
            UsState.NH,
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
            UsState.NH,
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
            UsState.NH,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var step = Assert.Single(result.WithholdingSteps!);
        Assert.Equal("No state income tax", step.Label);
        Assert.Equal(0m, step.Value);
        Assert.Contains("NH", result.WithholdingReference);
    }
}
