using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Idaho;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for <see cref="IdahoWithholdingCalculator"/>.
/// Expected dollar amounts are hand-computed from Idaho EPB00006's computer
/// formula:
///   annual taxable = max(0, (per-period wages × periods) − std deduction
///                              − ($3,300 × allowances))
///   annual tax     = annual taxable × 5.3%
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
/// </summary>
public class IdahoWithholdingCalculatorTest
{
    // ── State identity ─────────────────────────────────────────────

    [Fact]
    public void State_ReturnsIdaho()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.ID, calc.State);
    }

    // ── Schema ─────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithSingleAndMarriedOptions()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("ID W-4 Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal(IdahoWithholdingCalculator.StatusSingle, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains(IdahoWithholdingCalculator.StatusSingle, field.Options);
        Assert.Contains(IdahoWithholdingCalculator.StatusMarried, field.Options);
    }

    [Fact]
    public void Schema_ContainsAllowances()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "Allowances");
        Assert.Equal("ID W-4 Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Something Else" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeNumericFields_ReturnErrors()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle,
            ["Allowances"] = -1,
            ["AdditionalWithholding"] = -5m
        };
        var errors = calc.Validate(values);
        Assert.Equal(2, errors.Count);
    }

    // ── Calculation ───────────────────────────────────────────────

    // Single biweekly: annual 52,000 − 16,100 = 35,900;
    // × 5.3% = 1,902.70; / 26 = 73.18 (rounded half-away-from-zero).
    [Fact]
    public void Single_Biweekly_NoAllowances_MatchesFormula()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 2_000m, // $52,000 / 26
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_000m, result.TaxableWages);
        // 1,902.70 / 26 = 73.180769... ≈ 73.18
        Assert.Equal(73.18m, result.Withholding);
    }

    // Married biweekly: annual 78,000 − 32,200 = 45,800;
    // × 5.3% = 2,427.40; / 26 = 93.36.
    [Fact]
    public void Married_Biweekly_AppliesLargerStandardDeduction()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 3_000m, // $78,000 / 26
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusMarried
        };

        var result = calc.Calculate(context, values);

        // 2,427.40 / 26 = 93.361538... ≈ 93.36
        Assert.Equal(93.36m, result.Withholding);
    }

    // Allowances reduce annual taxable income by $3,300 each.
    // Annual 52,000 − 16,100 − (2 × 3,300) = 29,300; × 5.3% = 1,552.90;
    // / 26 = 59.73.
    [Fact]
    public void Allowances_ReduceTaxableIncome()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle,
            ["Allowances"] = 2
        };

        var result = calc.Calculate(context, values);

        // 1,552.90 / 26 = 59.726923... ≈ 59.73
        Assert.Equal(59.73m, result.Withholding);
    }

    // Pre-tax deductions reduce state taxable wages before annualization.
    // Per-period wages = 2,000 − 200 = 1,800; annual 46,800 − 16,100 = 30,700;
    // × 5.3% = 1,627.10; / 26 = 62.58.
    [Fact]
    public void PreTaxDeductions_ReducePerPeriodTaxableWages()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(1_800m, result.TaxableWages);
        // 1,627.10 / 26 = 62.580769... ≈ 62.58
        Assert.Equal(62.58m, result.Withholding);
    }

    // Low-income exemption: annual wages below the standard deduction
    // yield no income tax withholding.
    [Fact]
    public void LowWages_ZeroedOutByStandardDeduction_WithholdsZero()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 200m, // annual = 10,400, below $16,100 std ded
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(200m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // Additional withholding is added on top of the computed amount.
    [Fact]
    public void AdditionalWithholding_IsAddedAfterCalculation()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // $73.18 computed + $25 extra = $98.18
        Assert.Equal(73.18m + 25m, result.Withholding);
    }

    // Annual frequency short-circuits: annual tax equals the rounded result.
    // Single $60,000: 60,000 − 16,100 = 43,900; × 5.3% = $2,326.70.
    [Fact]
    public void AnnualFrequency_ReturnsAnnualTax()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 60_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_326.70m, result.Withholding);
    }

    // Combined scenario: married semimonthly with allowance, pre-tax
    // deduction, and extra withholding.
    // Per-period wages = 3,500 − 200 = 3,300; annual = 79,200;
    // − 32,200 − (1 × 3,300) = 43,700; × 5.3% = 2,316.10;
    // / 24 = 96.504166... ≈ 96.50; + $15 extra = 111.50.
    [Fact]
    public void CombinedScenario_Married_Allowance_PreTax_ExtraWithholding()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 3_500m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusMarried,
            ["Allowances"] = 1,
            ["AdditionalWithholding"] = 15m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(3_300m, result.TaxableWages);
        Assert.Equal(111.50m, result.Withholding);
    }

    // Zero gross wages → zero withholding.
    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // Pre-tax deductions exceeding gross wages floor state wages at zero.
    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // Idaho does not impose state disability insurance.
    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new IdahoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.ID,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = IdahoWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.DisabilityInsurance);
    }
}
