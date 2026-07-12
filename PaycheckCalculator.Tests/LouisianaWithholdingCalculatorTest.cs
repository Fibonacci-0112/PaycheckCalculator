using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Louisiana;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for <see cref="LouisianaWithholdingCalculator"/>.
/// Expected dollar amounts are hand-computed from Louisiana's R-1306
/// annualized percentage-method formula:
///   annual taxable = (per-period wages × periods)
///                    − personal exemption ($4,500 Single / $9,000 Married/HoH)
///                    − ($1,000 × dependents)
///   annual tax     = Louisiana graduated brackets applied to max(0, annual taxable)
///   per-period     = round(annual tax ÷ periods, 2) + line-7 extra
///
/// Louisiana graduated brackets:
///   Single:              1.85% on $0–$12,500 | 3.50% on $12,501–$50,000 | 4.25% over $50,000
///   Married / HoH:       1.85% on $0–$25,000 | 3.50% on $25,001–$100,000 | 4.25% over $100,000
/// </summary>
public class LouisianaWithholdingCalculatorTest
{
    // ── State identity ─────────────────────────────────────────────

    [Fact]
    public void State_ReturnsLouisiana()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.LA, calc.State);
    }

    // ── Schema ─────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithThreeOptions()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("L-4 Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal(LouisianaWithholdingCalculator.StatusSingle, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains(LouisianaWithholdingCalculator.StatusSingle, field.Options);
        Assert.Contains(LouisianaWithholdingCalculator.StatusMarried, field.Options);
        Assert.Contains(LouisianaWithholdingCalculator.StatusHeadOfHousehold, field.Options);
    }

    [Fact]
    public void Schema_ContainsDependents()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "Dependents");
        Assert.Equal("Dependents (Line 6B)", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding (Line 7)", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married")]
    [InlineData("Head of Household")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Exempt" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeDependents_ReturnsError()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle,
            ["Dependents"] = -1
        };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Dependents", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = -5m
        };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    // ── Calculation: Single ───────────────────────────────────────

    // Single, biweekly $3,000:
    //   annual = 3,000 × 26 = $78,000
    //   personal exemption = $4,500
    //   taxable = $73,500
    //   tax = 12,500 × 1.85% + 37,500 × 3.50% + 23,500 × 4.25%
    //       = 231.25 + 1,312.50 + 998.75 = $2,542.50
    //   per period = 2,542.50 / 26 = 97.7884... → $97.79
    [Fact]
    public void Single_Biweekly_SpansAllThreeBrackets_MatchesFormula()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(97.79m, result.Withholding);
    }

    // Single, monthly $800:
    //   annual = 800 × 12 = $9,600
    //   personal exemption = $4,500
    //   taxable = $5,100 (within first bracket only)
    //   tax = 5,100 × 1.85% = $94.35
    //   per period = 94.35 / 12 = 7.8625 → $7.86
    [Fact]
    public void Single_LowIncome_FirstBracketOnly()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 800m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        // 94.35 / 12 = 7.8625 → rounds half-away-from-zero to 7.86
        Assert.Equal(7.86m, result.Withholding);
    }

    // Single, below personal exemption → $0 withholding.
    //   biweekly $150: annual = $3,900, taxable = max(0, 3,900 - 4,500) = $0
    [Fact]
    public void Single_BelowPersonalExemption_WithholdsZero()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 150m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Calculation: Married ──────────────────────────────────────

    // Married, biweekly $4,000, 2 dependents:
    //   annual = 4,000 × 26 = $104,000
    //   personal exemption = $9,000
    //   dependent deduction = 2 × $1,000 = $2,000
    //   taxable = $93,000
    //   tax = 25,000 × 1.85% + (93,000 - 25,000) × 3.50%
    //       = 462.50 + 68,000 × 0.035
    //       = 462.50 + 2,380.00 = $2,842.50
    //   per period = 2,842.50 / 26 = 109.3269... → $109.33
    [Fact]
    public void Married_Biweekly_WithDependents_SpansTwoBrackets()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 4_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusMarried,
            ["Dependents"] = 2
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(109.33m, result.Withholding);
    }

    // Married, annual $200,000, no dependents — spans all three married brackets:
    //   personal exemption = $9,000
    //   taxable = $191,000
    //   tax = 25,000 × 1.85% + 75,000 × 3.50% + 91,000 × 4.25%
    //       = 462.50 + 2,625.00 + 3,867.50 = $6,955.00
    //   per period (annual) = $6,955.00
    [Fact]
    public void Married_Annual_HighIncome_SpansAllThreeBrackets()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 200_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusMarried
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(6_955.00m, result.Withholding);
    }

    // ── Calculation: Head of Household ───────────────────────────

    // Head of Household uses married personal exemption ($9,000) and brackets.
    // Monthly $5,000:
    //   annual = 5,000 × 12 = $60,000
    //   personal exemption = $9,000 (married/HoH)
    //   taxable = $51,000
    //   tax = 25,000 × 1.85% + (51,000 - 25,000) × 3.50%
    //       = 462.50 + 26,000 × 0.035
    //       = 462.50 + 910.00 = $1,372.50
    //   per period = 1,372.50 / 12 = 114.375 → $114.38
    [Fact]
    public void HeadOfHousehold_UsesMarriedBracketsAndExemption()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusHeadOfHousehold
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(114.38m, result.Withholding);
    }

    // ── Dependents reduce taxable income ─────────────────────────

    // Single, biweekly $2,000, 3 dependents:
    //   annual = 52,000
    //   personal exemption = $4,500
    //   dependent deduction = 3 × $1,000 = $3,000
    //   taxable = 52,000 - 4,500 - 3,000 = $44,500
    //   tax = 12,500 × 1.85% + (44,500 - 12,500) × 3.50%
    //       = 231.25 + 32,000 × 0.035
    //       = 231.25 + 1,120.00 = $1,351.25
    //   per period = 1,351.25 / 26 = 51.9711... → $51.97
    [Fact]
    public void Dependents_ReduceAnnualTaxableIncome()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle,
            ["Dependents"] = 3
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(51.97m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────

    // Base from Single biweekly $3,000 = $97.79; plus $30 extra = $127.79.
    [Fact]
    public void AdditionalWithholding_IsAddedAfterCalculation()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = 30m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(97.79m + 30m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────

    // Single, biweekly $3,000 gross, $500 pre-tax deductions:
    //   taxable wages per period = 3,000 - 500 = $2,500
    //   annual = 2,500 × 26 = $65,000
    //   personal exemption = $4,500
    //   taxable = 65,000 - 4,500 = $60,500
    //   tax = 12,500 × 1.85% + 37,500 × 3.50% + 10,500 × 4.25%
    //       = 231.25 + 1,312.50 + 446.25 = $1,990.00
    //   per period = 1,990.00 / 26 = 76.5384... → $76.54
    [Fact]
    public void PreTaxDeductions_ReducePerPeriodTaxableWages()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(76.54m, result.Withholding);
    }

    // ── Zero wages edge case ──────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new LouisianaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.LA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = LouisianaWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── LA is not in the generic percentage-method configs ────────

    [Fact]
    public void Louisiana_NotIn_StateTaxConfigs2026()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.LA),
            "LA should not be in StateTaxConfigs2026 — it uses LouisianaWithholdingCalculator.");
    }
}
