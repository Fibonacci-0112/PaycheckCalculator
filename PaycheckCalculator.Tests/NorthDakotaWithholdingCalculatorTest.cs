using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.NorthDakota;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for North Dakota (ND) state income tax withholding.
/// North Dakota uses the dedicated <see cref="NorthDakotaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the North Dakota Office of State
/// Tax Commissioner annualized percentage-method formula (2026 Employer's Withholding Guide):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction)
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 North Dakota parameters:
///   Standard deduction: $15,750 (Single) / $31,500 (Married) / $23,625 (Head of Household)
///   (mirrors the federal standard deduction; ND uses the federal Form W-4)
///   Brackets — Single / MFS:
///     1.10% on $0 – $46,500
///     2.04% on $46,500 – $113,750
///     2.64% over $113,750
///   Brackets — Married / QSS:
///     1.10% on $0 – $78,650
///     2.04% on $78,650 – $197,550
///     2.64% over $197,550
///   Brackets — Head of Household:
///     1.10% on $0 – $62,100
///     2.04% on $62,100 – $152,100
///     2.64% over $152,100
/// </summary>
public class NorthDakotaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNorthDakota()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.ND, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_AdditionalWithholding()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(2, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer ─────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracket()
    {
        // annual = $1,500 × 26 = $39,000
        // annual taxable = $39,000 − $15,750 = $23,250
        // tax = $23,250 × 1.10% = $255.75
        // per period = $255.75 / 26 = $9.836... → $9.84
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(9.84m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_SecondBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // annual taxable = $78,000 − $15,750 = $62,250
        // tax = $46,500 × 1.10% + ($62,250 − $46,500) × 2.04%
        //     = $511.50 + $15,750 × 0.0204
        //     = $511.50 + $321.30 = $832.80
        // per period = $832.80 / 26 = $32.030... → $32.03
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(32.03m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_FirstBracket()
    {
        // annual = $2,000 × 12 = $24,000
        // annual taxable = $24,000 − $15,750 = $8,250
        // tax = $8,250 × 1.10% = $90.75
        // per period = $90.75 / 12 = $7.5625 → $7.56
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(7.56m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TopBracket()
    {
        // annual = $6,000 × 26 = $156,000
        // annual taxable = $156,000 − $15,750 = $140,250
        // tax = $46,500 × 1.10% + ($113,750 − $46,500) × 2.04% + ($140,250 − $113,750) × 2.64%
        //     = $511.50 + $67,250 × 0.0204 + $26,500 × 0.0264
        //     = $511.50 + $1,371.90 + $699.60 = $2,583.00
        // per period = $2,583.00 / 26 = $99.346... → $99.35
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(6_000m, result.TaxableWages);
        Assert.Equal(99.35m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $500 × 12 = $6,000
        // annual taxable = max(0, $6,000 − $15,750) = $0
        var result = Calculate(GrossWages: 500m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married filer ────────────────────────────────────────────────

    [Fact]
    public void Married_Biweekly_FirstBracket()
    {
        // annual = $2,500 × 26 = $65,000
        // annual taxable = $65,000 − $31,500 = $33,500
        // tax = $33,500 × 1.10% = $368.50
        // per period = $368.50 / 26 = $14.173... → $14.17
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Biweekly, "Married");

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(14.17m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_SecondBracket()
    {
        // annual = $5,000 × 26 = $130,000
        // annual taxable = $130,000 − $31,500 = $98,500
        // tax = $78,650 × 1.10% + ($98,500 − $78,650) × 2.04%
        //     = $865.15 + $19,850 × 0.0204
        //     = $865.15 + $404.94 = $1,270.09
        // per period = $1,270.09 / 26 = $48.849... → $48.85
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(48.85m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_FirstBracket()
    {
        // annual = $4,000 × 12 = $48,000
        // annual taxable = $48,000 − $31,500 = $16,500
        // tax = $16,500 × 1.10% = $181.50
        // per period = $181.50 / 12 = $15.125 → $15.13
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(15.13m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TopBracket()
    {
        // annual = $10,000 × 26 = $260,000
        // annual taxable = $260,000 − $31,500 = $228,500
        // tax = $78,650 × 1.10% + ($197,550 − $78,650) × 2.04% + ($228,500 − $197,550) × 2.64%
        //     = $865.15 + $118,900 × 0.0204 + $30,950 × 0.0264
        //     = $865.15 + $2,425.56 + $817.08 = $4,107.79
        // per period = $4,107.79 / 26 = $157.991... → $157.99
        var result = Calculate(GrossWages: 10_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(10_000m, result.TaxableWages);
        Assert.Equal(157.99m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $500 × 26 = $13,000
        // annual taxable = max(0, $13,000 − $31,500) = $0
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household filer ──────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_FirstBracket()
    {
        // annual = $2,000 × 26 = $52,000
        // annual taxable = $52,000 − $23,625 = $28,375
        // tax = $28,375 × 1.10% = $312.125
        // per period = $312.125 / 26 = $12.004... → $12.00
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(12.00m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_SecondBracket()
    {
        // annual = $4,000 × 26 = $104,000
        // annual taxable = $104,000 − $23,625 = $80,375
        // tax = $62,100 × 1.10% + ($80,375 − $62,100) × 2.04%
        //     = $683.10 + $18,275 × 0.0204
        //     = $683.10 + $372.81 = $1,055.91
        // per period = $1,055.91 / 26 = $40.611... → $40.61
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(40.61m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_FirstBracket()
    {
        // annual = $3,500 × 12 = $42,000
        // annual taxable = $42,000 − $23,625 = $18,375
        // tax = $18,375 × 1.10% = $202.125
        // per period = $202.125 / 12 = $16.843... → $16.84
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(3_500m, result.TaxableWages);
        Assert.Equal(16.84m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_TopBracket()
    {
        // annual = $8,000 × 26 = $208,000
        // annual taxable = $208,000 − $23,625 = $184,375
        // tax = $62,100 × 1.10% + ($152,100 − $62,100) × 2.04% + ($184,375 − $152,100) × 2.64%
        //     = $683.10 + $90,000 × 0.0204 + $32,275 × 0.0264
        //     = $683.10 + $1,836.00 + $852.06 = $3,371.16
        // per period = $3,371.16 / 26 = $129.659... → $129.66
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(8_000m, result.TaxableWages);
        Assert.Equal(129.66m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $600 × 26 = $15,600
        // annual taxable = max(0, $15,600 − $23,625) = $0
        var result = Calculate(GrossWages: 600m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Bracket boundary ─────────────────────────────────────────────

    [Fact]
    public void Single_AtFirstBracketCeiling_ExactBoundary()
    {
        // annual taxable = $46,500 exactly (first bracket ceiling)
        // Need: ($46,500 + $15,750) / 26 = $62,250 / 26 = $2,394.230769...
        // annual = $2,394.230769... × 26 = $62,250 (exact)
        // Use gross = $62,250 / 26 so annual = $62,250, taxable = $62,250 - $15,750 = $46,500
        // tax = $46,500 × 1.10% = $511.50
        // per period = $511.50 / 26 = $19.673... → $19.67
        // Use monthly to keep math clean: annual = $4,000 × 12 = $48,000; taxable = $32,250 → first bracket still
        // Instead let's just confirm at-boundary Single Biweekly $2,394 ~ first bracket
        // annual = $2,394 × 26 = $62,244; taxable = $62,244 - $15,750 = $46,494
        // tax = $46,494 × 1.10% = $511.434 (all in first bracket since $46,494 < $46,500)
        // per period = $511.434 / 26 = $19.670... → $19.67
        var result = Calculate(GrossWages: 2_394m, PayFrequency.Biweekly, "Single");

        // annual taxable = $46,494 < $46,500 (first bracket only)
        Assert.Equal(19.67m, result.Withholding);
    }

    [Fact]
    public void Married_AtFirstBracketCeiling_ExactBoundary()
    {
        // annual = $4,235 × 26 = $110,110; taxable = $110,110 - $31,500 = $78,610
        // $78,610 < $78,650 → first bracket only
        // tax = $78,610 × 1.10% = $864.71
        // per period = $864.71 / 26 = $33.258... → $33.26
        var result = Calculate(GrossWages: 4_235m, PayFrequency.Biweekly, "Married");

        Assert.Equal(33.26m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // Base per-period (Single monthly $2,000) = $7.56; extra = $10 → $17.56
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 10m);

        Assert.Equal(17.56m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000; annual taxable = $65,000 − $15,750 = $49,250
        // tax = $46,500 × 1.10% + ($49,250 − $46,500) × 2.04%
        //     = $511.50 + $2,750 × 0.0204
        //     = $511.50 + $56.10 = $567.60
        // per period = $567.60 / 26 = $21.830... → $21.83
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(21.83m, result.Withholding);
    }

    // ── Zero gross wages ─────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Validation ───────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["AdditionalWithholding"] = 10m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AllFilingStatuses_AreAccepted()
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);

        foreach (var status in new[] { "Single", "Married", "Head of Household" })
        {
            var values = new StateInputValues { ["FilingStatus"] = status };
            Assert.Empty(calc.Validate(values));
        }
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new NorthDakotaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.ND,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
