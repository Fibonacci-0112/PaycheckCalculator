using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Georgia;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for <see cref="GeorgiaWithholdingCalculator"/>.
/// Expected dollar amounts are hand-computed from Georgia's 2026 Employer's
/// Tax Guide / Form G-4 percentage-method formula:
///   annual taxable = (per-period wages × periods) − std deduction
///                    − ($4,000 × dependents) − ($3,000 × additional allowances)
///   annual tax     = max(0, annual taxable) × 5.19%
///   per-period     = round(annual tax ÷ periods, 2) + line-6 extra
/// </summary>
public class GeorgiaWithholdingCalculatorTest
{
    // ── State identity ─────────────────────────────────────────────

    [Fact]
    public void State_ReturnsGeorgia()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.GA, calc.State);
    }

    // ── Schema ─────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithFiveOptions()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("G-4 Filing Status (Line 3)", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal(GeorgiaWithholdingCalculator.StatusA, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(5, field.Options!.Count);
        Assert.Contains(GeorgiaWithholdingCalculator.StatusA, field.Options);
        Assert.Contains(GeorgiaWithholdingCalculator.StatusB, field.Options);
        Assert.Contains(GeorgiaWithholdingCalculator.StatusC, field.Options);
        Assert.Contains(GeorgiaWithholdingCalculator.StatusD, field.Options);
        Assert.Contains(GeorgiaWithholdingCalculator.StatusExempt, field.Options);
    }

    [Fact]
    public void Schema_ContainsDependents()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "Dependents");
        Assert.Equal("Dependents (Line 4)", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalAllowances()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "AdditionalAllowances");
        Assert.Equal("Additional Allowances (Line 5)", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding (Line 6)", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("A — Single")]
    [InlineData("B — Married Filing Separately / Both Spouses Working")]
    [InlineData("C — Married Filing Jointly, One Spouse Working")]
    [InlineData("D — Head of Household")]
    [InlineData("Exempt")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Something Else" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeNumericFields_ReturnErrors()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA,
            ["Dependents"] = -1,
            ["AdditionalAllowances"] = -2,
            ["AdditionalWithholding"] = -5m
        };
        var errors = calc.Validate(values);
        Assert.Equal(3, errors.Count);
    }

    // ── Calculation ───────────────────────────────────────────────

    // Status A, Single: annual wages = 52,000, std ded = 12,000,
    // taxable = 40,000, annual tax = 40,000 × 5.19% = $2,076.00,
    // biweekly (26) = $79.85 (rounded half-away-from-zero).
    [Fact]
    public void StatusA_Single_Biweekly_NoDependents_MatchesFormula()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m, // $52,000 / 26
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_000m, result.TaxableWages);
        // 40,000 × 0.0519 = 2,076.00; 2,076.00 / 26 = 79.846153... ≈ 79.85
        Assert.Equal(79.85m, result.Withholding);
    }

    // Status C, MFJ one spouse working: annual wages = $78,000, std ded =
    // $24,000, taxable = $54,000, tax = 54,000 × 0.0519 = $2,802.60,
    // biweekly = 2,802.60 / 26 = $107.79 (rounded).
    [Fact]
    public void StatusC_MarriedJointOneSpouse_AppliesLargerStandardDeduction()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 3_000m, // $78,000 / 26
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusC
        };

        var result = calc.Calculate(context, values);

        // 2,802.60 / 26 = 107.7923... ≈ 107.79
        Assert.Equal(107.79m, result.Withholding);
    }

    // Status B, MFS or MFJ both-working: uses single-sized std deduction.
    [Fact]
    public void StatusB_UsesSingleStandardDeduction()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusB
        };

        var result = calc.Calculate(context, values);

        // Annual 78,000 − 12,000 = 66,000; × 5.19% = 3,425.40; / 26 = 131.75
        // 3,425.40 / 26 = 131.7461... ≈ 131.75
        Assert.Equal(131.75m, result.Withholding);
    }

    // Status D, HoH: $12,000 standard deduction (same as Single).
    [Fact]
    public void StatusD_HeadOfHousehold_UsesSingleStandardDeduction()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusD
        };

        var result = calc.Calculate(context, values);

        // Same as Status A (Single): biweekly = $79.85
        Assert.Equal(79.85m, result.Withholding);
    }

    // Dependents reduce annual taxable income by $4,000 each.
    // Annual 52,000 − 12,000 − (2 × 4,000) = 32,000; × 5.19% = 1,660.80;
    // / 26 = 63.88.
    [Fact]
    public void Dependents_ReduceTaxableIncome()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA,
            ["Dependents"] = 2
        };

        var result = calc.Calculate(context, values);

        // 1,660.80 / 26 = 63.8769... ≈ 63.88
        Assert.Equal(63.88m, result.Withholding);
    }

    // Additional allowances (Line 5) reduce annual taxable by $3,000 each.
    // Annual 52,000 − 12,000 − (2 × 3,000) = 34,000; × 5.19% = 1,764.60;
    // / 26 = 67.87.
    [Fact]
    public void AdditionalAllowances_ReduceTaxableIncome()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA,
            ["AdditionalAllowances"] = 2
        };

        var result = calc.Calculate(context, values);

        // 1,764.60 / 26 = 67.8692... ≈ 67.87
        Assert.Equal(67.87m, result.Withholding);
    }

    // Additional withholding is added on top of the computed amount.
    [Fact]
    public void AdditionalWithholding_IsAddedAfterCalculation()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(79.85m + 25m, result.Withholding);
    }

    // Exempt (Line 7) → no income tax withheld regardless of other fields.
    [Fact]
    public void Exempt_WithholdsZero()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 5_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusExempt,
            ["Dependents"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Contains("Exempt", result.Description);
    }

    // Very low wages → allowances zero out taxable income; withholding = $0.
    [Fact]
    public void LowWages_ZeroOutByAllowances_WithholdsZero()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 400m, // annual = 10,400
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA,
            // Annual 10,400 − 12,000 std ded goes negative → clamp to 0.
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
    }

    // Pre-tax deductions reduce state taxable wages before annualization.
    // Per-period wages = 2,000 − 200 = 1,800; annual = 46,800 − 12,000 =
    // 34,800; × 5.19% = 1,806.12; / 26 = 69.47.
    [Fact]
    public void PreTaxDeductions_ReducePerPeriodTaxableWages()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(1_800m, result.TaxableWages);
        // 1,806.12 / 26 = 69.4661... ≈ 69.47
        Assert.Equal(69.47m, result.Withholding);
    }

    // Annual frequency short-circuits: annual tax equals the rounded result.
    // $80,000 Single: 80,000 − 12,000 = 68,000; × 5.19% = $3,529.20.
    [Fact]
    public void AnnualFrequency_ReturnsAnnualTax()
    {
        var calc = new GeorgiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.GA,
            GrossWages: 80_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = GeorgiaWithholdingCalculator.StatusA
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(3_529.20m, result.Withholding);
    }
}
