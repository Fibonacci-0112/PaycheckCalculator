using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.Wyoming;
using Xunit;

/// <summary>
/// Wyoming has no individual income tax (see Wyo. Const. art. 15, § 18 and the
/// Wyoming Department of Revenue taxation-structure page) and no employee-paid
/// state payroll assessments.  The dedicated WyomingWithholdingCalculator exposes
/// an empty schema and always returns zero withholding.
/// </summary>
public class WyomingWithholdingCalculatorTest
{
    private static WyomingWithholdingCalculator CreateCalc() => new();

    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsWyoming()
    {
        var calc = CreateCalc();
        Assert.Equal(UsState.WY, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_IsEmpty()
    {
        // Wyoming has no income tax and no employee-paid payroll assessments,
        // so no state-specific input fields are needed.
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
        // Wyoming levies no income tax on wages, so withholding is always $0.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WY,
            GrossWages: 5_000m,
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
            UsState.WY,
            GrossWages: 25_000m,
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
            UsState.WY,
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
        // Pre-tax deductions have no effect because there is no WY income tax to begin with.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WY,
            GrossWages: 4_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1_000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.Withholding);
    }

    // ── Description ─────────────────────────────────────────────────

    [Fact]
    public void Description_IndicatesNoStateTax()
    {
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WY,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal("No state income tax", result.Description);
    }

    // ── No disability insurance ─────────────────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        // Wyoming has no employee-paid state payroll assessments.
        var calc = CreateCalc();

        var context = new CommonWithholdingContext(
            UsState.WY,
            GrossWages: 5_000m,
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
            UsState.WY,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.NotNull(result.WithholdingSteps);
        var step = Assert.Single(result.WithholdingSteps!);
        Assert.Equal("No state income tax", step.Label);
        Assert.Equal(0m, step.Value);
    }
}
