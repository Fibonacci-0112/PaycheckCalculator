using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Mississippi;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for Mississippi (MS) state income tax withholding.
/// Mississippi uses the dedicated <see cref="MississippiWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Mississippi Department of
/// Revenue annualized percentage-method formula (Pub. 89-105, Form 89-350, 2026):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − personal exemption
///                         − (dependents × $1,500))
///   annual tax     = max(0, annual taxable − $10,000) × 4%
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Mississippi parameters:
///   Standard deduction:  $2,300 (Single) / $4,600 (Married) / $3,400 (Head of Household)
///   Personal exemption:  $6,000 (Single) / $12,000 (Married) / $9,500 (Head of Household)
///   Dependent exemption: $1,500 per dependent (Form 89-350 Line 6)
///   Brackets:            0% on $0–$10,000 | 4% over $10,000
/// </summary>
public class MississippiWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMississippi()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.MS, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Dependents_AdditionalWithholding()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Dependents");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_ThreeOptions()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("89-350 Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal(MississippiWithholdingCalculator.StatusSingle, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains(MississippiWithholdingCalculator.StatusSingle, field.Options);
        Assert.Contains(MississippiWithholdingCalculator.StatusMarried, field.Options);
        Assert.Contains(MississippiWithholdingCalculator.StatusHeadOfHousehold, field.Options);
    }

    [Fact]
    public void Schema_Dependents_IsIntegerDefaultZero()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "Dependents");

        Assert.Equal("Dependents (Line 6)", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_AdditionalWithholding_IsDecimalDefaultZero()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "AdditionalWithholding");

        Assert.Equal("Additional Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married")]
    [InlineData("Head of Household")]
    public void Validate_ValidFilingStatuses_ReturnNoErrors(string status)
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "Exempt" };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeDependents_ReturnsError()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = MississippiWithholdingCalculator.StatusSingle,
            ["Dependents"] = -1
        };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Dependents", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = MississippiWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = -10m
        };
        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    // ── Calculation: Single ─────────────────────────────────────────

    // Single, biweekly $3,000, no dependents:
    //   annual wages = $3,000 × 26 = $78,000
    //   less std ded ($2,300) + personal exemption ($6,000) = $69,700
    //   over $10,000 threshold: $69,700 − $10,000 = $59,700
    //   annual tax = $59,700 × 4% = $2,388.00
    //   per period = $2,388.00 / 26 = $91.846154... → $91.85
    [Fact]
    public void Single_Biweekly_NoDependents_CorrectWithholding()
    {
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(91.85m, result.Withholding);
    }

    // Single, below total exemptions → $0 withholding.
    //   biweekly $300: annual = $7,800
    //   $7,800 − $2,300 − $6,000 = −$500 → max(0, −$500) = $0
    [Fact]
    public void Single_BelowExemptions_WithholdsZero()
    {
        var result = Calculate(GrossWages: 300m, PayFrequency.Biweekly, "Single");

        Assert.Equal(300m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // Single, annual taxable income exactly at $10,000 → $0 tax (entirely in 0% bracket).
    //   monthly $1,525: annual = $18,300
    //   $18,300 − $2,300 − $6,000 = $10,000
    //   annual tax = max(0, $10,000 − $10,000) × 4% = $0
    [Fact]
    public void Single_AnnualTaxableAtZeroRateCeiling_WithholdsZero()
    {
        var result = Calculate(GrossWages: 1_525m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_525m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // Single, annual taxable income just above $10,000 threshold.
    //   monthly $1,526: annual = $18,312
    //   $18,312 − $2,300 − $6,000 = $10,012
    //   annual tax = ($10,012 − $10,000) × 4% = $12 × 0.04 = $0.48
    //   per period = $0.48 / 12 = $0.04
    [Fact]
    public void Single_JustAboveZeroRateCeiling_SmallWithholding()
    {
        var result = Calculate(GrossWages: 1_526m, PayFrequency.Monthly, "Single");

        Assert.Equal(0.04m, result.Withholding);
    }

    // ── Calculation: Married ────────────────────────────────────────

    // Married, monthly $5,000, no dependents:
    //   annual wages = $5,000 × 12 = $60,000
    //   less std ded ($4,600) + personal exemption ($12,000) = $43,400
    //   over $10,000 threshold: $43,400 − $10,000 = $33,400
    //   annual tax = $33,400 × 4% = $1,336.00
    //   per period = $1,336.00 / 12 = $111.333... → $111.33
    [Fact]
    public void Married_Monthly_NoDependents_CorrectWithholding()
    {
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(111.33m, result.Withholding);
    }

    // Married, biweekly $4,000, 3 dependents:
    //   annual wages = $4,000 × 26 = $104,000
    //   less std ded ($4,600) + personal exemption ($12,000) + 3 × $1,500 = $82,900
    //   over $10,000: $82,900 − $10,000 = $72,900
    //   annual tax = $72,900 × 4% = $2,916.00
    //   per period = $2,916.00 / 26 = $112.153846... → $112.15
    [Fact]
    public void Married_Biweekly_ThreeDependents_CorrectWithholding()
    {
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Married",
            dependents: 3);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(112.15m, result.Withholding);
    }

    // Married, below combined exemptions → $0 withholding.
    //   biweekly $600: annual = $15,600
    //   $15,600 − $4,600 − $12,000 = −$1,000 → $0
    [Fact]
    public void Married_BelowExemptions_WithholdsZero()
    {
        var result = Calculate(GrossWages: 600m, PayFrequency.Biweekly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Calculation: Head of Household ─────────────────────────────

    // Head of Household, biweekly $2,000, 2 dependents:
    //   annual wages = $2,000 × 26 = $52,000
    //   less std ded ($3,400) + personal exemption ($9,500) + 2 × $1,500 = $36,100
    //   over $10,000: $36,100 − $10,000 = $26,100
    //   annual tax = $26,100 × 4% = $1,044.00
    //   per period = $1,044.00 / 26 = $40.153846... → $40.15
    [Fact]
    public void HeadOfHousehold_Biweekly_TwoDependents_CorrectWithholding()
    {
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            MississippiWithholdingCalculator.StatusHeadOfHousehold, dependents: 2);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(40.15m, result.Withholding);
    }

    // Head of Household, below combined exemptions → $0 withholding.
    //   biweekly $500: annual = $13,000
    //   $13,000 − $3,400 − $9,500 = $100 < $10,000 → 0% bracket → $0 tax
    [Fact]
    public void HeadOfHousehold_WithinZeroRateBracket_WithholdsZero()
    {
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly,
            MississippiWithholdingCalculator.StatusHeadOfHousehold);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Dependent exemption ─────────────────────────────────────────

    // Single, biweekly $3,000, 2 dependents ($1,500 each):
    //   annual wages = $78,000
    //   less $2,300 std + $6,000 personal + 2 × $1,500 dependents = $11,300
    //   taxable = $78,000 − $11,300 = $66,700
    //   over $10,000: $66,700 − $10,000 = $56,700
    //   annual tax = $56,700 × 4% = $2,268.00
    //   per period = $2,268.00 / 26 = $87.230769... → $87.23
    [Fact]
    public void Single_TwoDependents_ReducesTaxCorrectly()
    {
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            dependents: 2);

        Assert.Equal(87.23m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    // Single, biweekly $3,000, extra $20 per period:
    //   base withholding = $91.85 (see Single_Biweekly_NoDependents_CorrectWithholding)
    //   total = $91.85 + $20 = $111.85
    [Fact]
    public void Single_ExtraWithholding_IsAdded()
    {
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 20m);

        Assert.Equal(111.85m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    // Single, biweekly $3,000, pre-tax deductions $500:
    //   per-period taxable wages = $3,000 − $500 = $2,500
    //   annual = $2,500 × 26 = $65,000
    //   less $2,300 + $6,000 = $56,700
    //   over $10,000: $56,700 − $10,000 = $46,700
    //   annual tax = $46,700 × 4% = $1,868.00
    //   per period = $1,868.00 / 26 = $71.846154... → $71.85
    [Fact]
    public void Single_PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.MS,
            GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = MississippiWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(71.85m, result.Withholding);
    }

    // ── High-income rounding check ──────────────────────────────────

    // Single, annual pay period, gross $200,000:
    //   after exemptions = $200,000 − $2,300 − $6,000 = $191,700
    //   annual tax = ($191,700 − $10,000) × 4% = $181,700 × 0.04 = $7,268.00
    //   per period (annual) = $7,268.00 / 1 = $7,268.00
    [Fact]
    public void Single_Annual_HighIncome_CorrectWithholding()
    {
        var result = Calculate(GrossWages: 200_000m, PayFrequency.Annual, "Single");

        Assert.Equal(7_268.00m, result.Withholding);
    }

    // ── Helper ──────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency payFrequency,
        string filingStatus,
        int dependents = 0,
        decimal additionalWithholding = 0m)
    {
        var calc = new MississippiWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            State: UsState.MS,
            GrossWages: GrossWages,
            PayPeriod: payFrequency,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["Dependents"] = dependents,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
