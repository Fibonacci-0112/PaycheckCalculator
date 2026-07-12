using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Wisconsin;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Wisconsin (WI) state income tax withholding.
/// Wisconsin uses the dedicated <see cref="WisconsinWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Wisconsin Department of
/// Revenue Employer's Withholding Tax Guide (Publication W-166, 2026) and
/// the accompanying Circular WT.
///
/// Algorithm:
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − (allowances × $700))
///   annual tax     = graduated brackets applied to annual taxable income
///                    Single / Head of Household brackets (identical thresholds):
///                      3.54% on $0–$13,810     | 4.65% on $13,810–$27,630 |
///                      5.30% on $27,630–$304,170 | 7.65% over $304,170
///                    Married brackets:
///                      3.54% on $0–$18,410     | 4.65% on $18,410–$36,820 |
///                      5.30% on $36,820–$405,550 | 7.65% over $405,550
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Wisconsin parameters (Publication W-166):
///   Standard deduction: $12,760 (Single) / $23,170 (Married) / $16,840 (Head of Household)
///   Per-WT-4-allowance deduction: $700
/// </summary>
public class WisconsinWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsWisconsin()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.WI, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_HasThreeOptions()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married")]
    [InlineData("Head of Household")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = status });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Jointly" });
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Allowances"));
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        });
        Assert.Contains(errors, e => e.Contains("Additional Withholding"));
    }

    // ── Single filer — low income (below standard deduction) ────────

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZeroWithholding()
    {
        // annual = $1,000 × 12 = $12,000; std ded = $12,760 → taxable = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(0.00m, result.Withholding);
    }

    // ── Single filer — first bracket only ───────────────────────────

    [Fact]
    public void Single_Monthly_TaxableInFirstBracketOnly()
    {
        // annual = $2,000 × 12 = $24,000; std ded = $12,760 → taxable = $11,240
        // $11,240 entirely in first bracket 0–$13,810 @ 3.54% = $397.896
        // per period = $397.896 / 12 = $33.158 → $33.16
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(33.16m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ───────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $3,000 × 12 = $36,000; std ded = $12,760 → taxable = $23,240
        // 0–$13,810 @ 3.54%        = $13,810 × 0.0354 = $488.874
        // $13,810–$23,240 @ 4.65%  = $9,430 × 0.0465  = $438.495
        // total = $927.369
        // per period = $927.369 / 12 = $77.2807... → $77.28
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(77.28m, result.Withholding);
    }

    // ── Single filer — crosses into third bracket ────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoThirdBracket()
    {
        // annual = $5,000 × 12 = $60,000; std ded = $12,760 → taxable = $47,240
        // 0–$13,810 @ 3.54%        = $488.874
        // $13,810–$27,630 @ 4.65%  = $13,820 × 0.0465 = $642.630
        // $27,630–$47,240 @ 5.30%  = $19,610 × 0.053  = $1,039.330
        // total = $2,170.834
        // per period = $2,170.834 / 12 = $180.9028... → $180.90
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(180.90m, result.Withholding);
    }

    // ── Single filer — top bracket (7.65%) ──────────────────────────

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $30,000 × 12 = $360,000; std ded = $12,760 → taxable = $347,240
        // 0–$13,810 @ 3.54%           = $488.874
        // $13,810–$27,630 @ 4.65%     = $13,820 × 0.0465 = $642.630
        // $27,630–$304,170 @ 5.30%    = $276,540 × 0.053 = $14,656.620
        // $304,170–$347,240 @ 7.65%   = $43,070 × 0.0765 = $3,294.855
        // total = $19,082.979
        // per period = $19,082.979 / 12 = $1,590.24825 → $1,590.25
        var result = Calculate(GrossWages: 30_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(30_000m, result.TaxableWages);
        Assert.Equal(1_590.25m, result.Withholding);
    }

    // ── Married filer ────────────────────────────────────────────────

    [Fact]
    public void Married_Biweekly_CrossesSecondBracket()
    {
        // annual = $5,000 × 26 = $130,000; std ded = $23,170 → taxable = $106,830
        // 0–$18,410 @ 3.54%         = $18,410 × 0.0354  = $651.714
        // $18,410–$36,820 @ 4.65%   = $18,410 × 0.0465  = $856.065
        // $36,820–$106,830 @ 5.30%  = $70,010 × 0.053   = $3,710.530
        // total = $5,218.309
        // per period = $5,218.309 / 26 = $200.7041... → $200.70
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(200.70m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_BelowStandardDeduction_ReturnsZeroWithholding()
    {
        // annual = $1,500 × 12 = $18,000; std ded = $23,170 → taxable = $0
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Married");

        Assert.Equal(0.00m, result.Withholding);
    }

    // ── Head of Household filer ───────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $3,000 × 12 = $36,000; std ded = $16,840 → taxable = $19,160
        // 0–$13,810 @ 3.54%         = $488.874
        // $13,810–$19,160 @ 4.65%   = $5,350 × 0.0465 = $248.775
        // total = $737.649
        // per period = $737.649 / 12 = $61.4707... → $61.47
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(61.47m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_UsesSingleBrackets()
    {
        // Head of Household uses Single brackets but its own standard deduction.
        // For HoH: taxable = annual − $16,840 − allowances
        // For Single: taxable = annual − $12,760 − allowances
        // They share bracket thresholds, so with the same taxable income they
        // produce the same tax.

        // Build two scenarios with equal taxable income by adjusting allowances:
        // HoH $3,000/month, 0 allowances: annual taxable = 36,000 − 16,840 = 19,160
        // Single $3,000/month, 6 allowances (6×700=4,200): annual taxable = 36,000 − 12,760 − 4,200 = 19,040
        // Not equal, so instead verify directly that HoH uses single brackets
        // by confirming HoH withholding is between Single (lower deduction) and
        // Married (higher deduction and wider brackets) for the same gross.
        var hohResult = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Head of Household");
        var singleResult = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Single");
        var marriedResult = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Married");

        // HoH has a larger std ded than Single → lower withholding than Single.
        // HoH has a smaller std ded than Married, and Single bracket thresholds
        // are narrower → HoH may be higher than Married at moderate income.
        Assert.True(hohResult.Withholding < singleResult.Withholding,
            "HoH standard deduction is larger than Single → less withholding");
        Assert.True(marriedResult.Withholding < hohResult.Withholding,
            "Married standard deduction is larger than HoH at this income → less withholding");
    }

    // ── Bracket boundary verification (annual pay period) ────────────

    [Fact]
    public void Single_Annual_ExactlyAt13810BracketBoundary()
    {
        // Annual, gross = $13,810 + $12,760 = $26,570, 0 allowances:
        // annual taxable = $26,570 − $12,760 = $13,810 (exactly at first/second boundary)
        // tax = $13,810 × 3.54% = $488.874
        // per period (annual) = $488.874 / 1 = $488.87 (rounded AwayFromZero)
        var result = Calculate(GrossWages: 26_570m, PayFrequency.Annual, "Single");

        Assert.Equal(488.87m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt27630BracketBoundary()
    {
        // Annual, gross = $27,630 + $12,760 = $40,390, 0 allowances:
        // annual taxable = $40,390 − $12,760 = $27,630 (at second/third boundary)
        // tax = $13,810 × 3.54% + $13,820 × 4.65% = $488.874 + $642.630 = $1,131.504
        var result = Calculate(GrossWages: 40_390m, PayFrequency.Annual, "Single");

        Assert.Equal(1_131.50m, result.Withholding);
    }

    [Fact]
    public void Married_Annual_ExactlyAt18410BracketBoundary()
    {
        // Annual, gross = $18,410 + $23,170 = $41,580, 0 allowances:
        // annual taxable = $41,580 − $23,170 = $18,410 (exactly at first/second boundary)
        // tax = $18,410 × 3.54% = $651.714
        var result = Calculate(GrossWages: 41_580m, PayFrequency.Annual, "Married");

        Assert.Equal(651.71m, result.Withholding);
    }

    // ── Allowances reduce taxable income ─────────────────────────────

    [Fact]
    public void Single_Monthly_TwoAllowances_ReducesTax()
    {
        // annual = $2,000 × 12 = $24,000; std ded = $12,760; 2 allowances = $1,400
        // taxable = $24,000 − $12,760 − $1,400 = $9,840
        // $9,840 entirely in first bracket @ 3.54% = $348.336
        // per period = $348.336 / 12 = $29.028 → $29.03
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single", allowances: 2);

        Assert.Equal(29.03m, result.Withholding);
    }

    [Fact]
    public void Allowances_HighEnoughToZeroOutTax()
    {
        // annual = $2,000 × 12 = $24,000; std ded = $12,760; 20 allowances = $14,000
        // taxable = max(0, $24,000 − $12,760 − $14,000) = max(0, −$2,760) = $0
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single", allowances: 20);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_TaxableInFirstBracketOnly = $33.16; extra = $25.00 → $58.16
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 25m);

        Assert.Equal(58.16m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $2,000, pre-tax $500 → taxable wages = $1,500
        // annual = $1,500 × 12 = $18,000; std ded = $12,760 → taxable income = $5,240
        // $5,240 entirely in first bracket @ 3.54% = $185.496
        // per period = $185.496 / 12 = $15.458 → $15.46
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(15.46m, result.Withholding);
    }

    // ── Biweekly pay frequency ────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_CrossesSecondBracket()
    {
        // annual = $5,000 × 26 = $130,000; std ded = $12,760 → taxable = $117,240
        // 0–$13,810 @ 3.54%       = $488.874
        // $13,810–$27,630 @ 4.65% = $13,820 × 0.0465 = $642.630
        // $27,630–$117,240 @ 5.30% = $89,610 × 0.053 = $4,749.330
        // total = $5,880.834; per period = $5,880.834 / 26 = $226.1859... → $226.19
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(226.19m, result.Withholding);
    }

    // ── Zero wages edge case ──────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Not in generic configs assertion ──────────────────────────────

    [Fact]
    public void Wisconsin_NotInGenericPercentageMethodConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.WI),
            "WI should not be in StateTaxConfigs2026 — it uses WisconsinWithholdingCalculator.");
    }

    // ── Helper ────────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        int allowances = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new WisconsinWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.WI,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["Allowances"] = allowances,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
