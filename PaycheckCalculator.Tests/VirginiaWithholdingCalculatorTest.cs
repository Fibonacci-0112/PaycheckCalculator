using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Virginia;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Virginia (VA) state income tax withholding.
/// Virginia uses the dedicated <see cref="VirginiaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Virginia Department of
/// Taxation Employer Withholding Instructions (Publication 93045, 2026).
///
/// Algorithm:
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − (exemptions × $930))
///   annual tax     = graduated brackets applied to annual taxable income:
///                    2.00% on $0–$3,000 | 3.00% on $3,001–$5,000 |
///                    5.00% on $5,001–$17,000 | 5.75% over $17,000
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Virginia parameters (Pub. 93045):
///   Standard deduction: $8,750 (Single); $17,500 (Married / Head of Household)
///   Per-exemption deduction: $930 (Form VA-4)
///   Bracket thresholds are the same for all filing statuses.
/// </summary>
public class VirginiaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsVirginia()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.VA, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Exemptions_AdditionalWithholding()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = status });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Exemptions"));
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        });
        Assert.Contains(errors, e => e.Contains("Additional Withholding"));
    }

    // ── Single filer — first bracket only ───────────────────────────

    [Fact]
    public void Single_Monthly_TaxableInFirstBracketOnly()
    {
        // annual = $800 × 12 = $9,600; std ded (Single) = $8,750 → taxable = $850
        // $850 entirely in first bracket 0–$3,000 @ 2.00% = $17.00
        // per period = $17.00 / 12 = $1.4166... → $1.42
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Single");

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(1.42m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ───────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $1,100 × 12 = $13,200; std ded = $8,750 → taxable = $4,450
        // 0–$3,000 @ 2.00%    = $60.00
        // $3,000–$4,450 @ 3.00% = $1,450 × 0.03 = $43.50
        // total = $103.50
        // per period = $103.50 / 12 = $8.625 → $8.63 (AwayFromZero)
        var result = Calculate(GrossWages: 1_100m, PayFrequency.Monthly, "Single");

        Assert.Equal(8.63m, result.Withholding);
    }

    // ── Single filer — crosses into third bracket ─────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoThirdBracket()
    {
        // annual = $2,000 × 12 = $24,000; std ded = $8,750 → taxable = $15,250
        // 0–$3,000 @ 2.00%          = $60.00
        // $3,000–$5,000 @ 3.00%     = $60.00
        // $5,000–$15,250 @ 5.00%    = $10,250 × 0.05 = $512.50
        // total = $632.50
        // per period = $632.50 / 12 = $52.7083... → $52.71
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(52.71m, result.Withholding);
    }

    // ── Single filer — top bracket (5.75%) ───────────────────────────

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $5,000 × 12 = $60,000; std ded = $8,750; exemptions = 0 → taxable = $51,250
        // 0–$3,000 @ 2.00%            = $60.00
        // $3,000–$5,000 @ 3.00%       = $60.00
        // $5,000–$17,000 @ 5.00%      = $600.00
        // $17,000–$51,250 @ 5.75%     = $34,250 × 0.0575 = $1,969.375
        // total = $2,689.375
        // per period = $2,689.375 / 12 = $224.1145... → $224.11
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(224.11m, result.Withholding);
    }

    // ── Married filer ────────────────────────────────────────────────

    [Fact]
    public void Married_Monthly_TaxableInFirstBracketOnly()
    {
        // annual = $1,500 × 12 = $18,000; std ded (Married) = $17,500 → taxable = $500
        // $500 entirely in first bracket @ 2.00% = $10.00
        // per period = $10.00 / 12 = $0.8333... → $0.83
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Married");

        Assert.Equal(0.83m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_TopBracket()
    {
        // annual = $8,000 × 12 = $96,000; std ded = $17,500; exemptions = 2 → taxable = $76,640
        //   ($96,000 − $17,500 − 2 × $930 = $76,640)
        // 0–$3,000 @ 2.00%            = $60.00
        // $3,000–$5,000 @ 3.00%       = $60.00
        // $5,000–$17,000 @ 5.00%      = $600.00
        // $17,000–$76,640 @ 5.75%     = $59,640 × 0.0575 = $3,429.30
        // total = $4,149.30
        // per period = $4,149.30 / 12 = $345.775 → $345.78 (AwayFromZero)
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Married", exemptions: 2);

        Assert.Equal(8_000m, result.TaxableWages);
        Assert.Equal(345.78m, result.Withholding);
    }

    // ── Head of Household filer (uses Married standard deduction $17,500) ──

    [Fact]
    public void HeadOfHousehold_UsesMarriedStandardDeduction()
    {
        // HoH uses $17,500 standard deduction (same as Married)
        // annual = $8,000 × 12 = $96,000; std ded = $17,500; exemptions = 2 → taxable = $76,640
        // Same computation as Married_Monthly_TopBracket → $345.78
        var resultHoH = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Head of Household", exemptions: 2);
        var resultMarried = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Married", exemptions: 2);

        // HoH and Married share the same $17,500 standard deduction and identical brackets,
        // so their withholding must be equal.
        Assert.Equal(resultMarried.Withholding, resultHoH.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_TopBracket()
    {
        // annual = $3,000 × 26 = $78,000; std ded (HoH) = $17,500; exemptions = 1 → taxable = $59,570
        //   ($78,000 − $17,500 − 1 × $930 = $59,570)
        // 0–$3,000 @ 2.00%            = $60.00
        // $3,000–$5,000 @ 3.00%       = $60.00
        // $5,000–$17,000 @ 5.00%      = $600.00
        // $17,000–$59,570 @ 5.75%     = $42,570 × 0.0575 = $2,447.775
        // total = $3,167.775
        // per period = $3,167.775 / 26 = $121.8375 → $121.84 (AwayFromZero midpoint rounds up)
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Head of Household", exemptions: 1);

        Assert.Equal(121.84m, result.Withholding);
    }

    // ── Single filer — bracket boundary verification ─────────────────

    [Fact]
    public void Single_Annual_ExactlyAt3000BracketBoundary()
    {
        // Annual, $11,750 gross, 0 exemptions:
        // std ded = $8,750 → taxable = $3,000 (exactly at first/second bracket boundary)
        // tax = $3,000 × 2.00% = $60.00
        // per period (annual) = $60.00 / 1 = $60.00
        var result = Calculate(GrossWages: 11_750m, PayFrequency.Annual, "Single");

        Assert.Equal(60.00m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt5000BracketBoundary()
    {
        // Annual, $13,750 gross, 0 exemptions:
        // std ded = $8,750 → taxable = $5,000 (at second/third bracket boundary)
        // tax = $3,000 × 2% + $2,000 × 3% = $60.00 + $60.00 = $120.00
        var result = Calculate(GrossWages: 13_750m, PayFrequency.Annual, "Single");

        Assert.Equal(120.00m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt17000BracketBoundary()
    {
        // Annual, $25,750 gross, 0 exemptions:
        // std ded = $8,750 → taxable = $17,000 (at third/top bracket boundary)
        // tax = $60 + $60 + $12,000 × 5% = $60 + $60 + $600 = $720.00
        var result = Calculate(GrossWages: 25_750m, PayFrequency.Annual, "Single");

        Assert.Equal(720.00m, result.Withholding);
    }

    // ── Exemptions ────────────────────────────────────────────────────

    [Fact]
    public void Single_Monthly_OneExemption_ReducesTax()
    {
        // annual = $5,000 × 12 = $60,000; std ded = $8,750; 1 exemption = $930 → taxable = $50,320
        // 0–$3,000 @ 2.00%            = $60.00
        // $3,000–$5,000 @ 3.00%       = $60.00
        // $5,000–$17,000 @ 5.00%      = $600.00
        // $17,000–$50,320 @ 5.75%     = $33,320 × 0.0575 = $1,915.90
        // total = $2,635.90
        // per period = $2,635.90 / 12 = $219.6583... → $219.66
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single", exemptions: 1);

        Assert.Equal(219.66m, result.Withholding);
    }

    [Fact]
    public void Exemptions_HighEnoughToZeroOutTax()
    {
        // annual = $800 × 12 = $9,600; std ded = $8,750; 5 exemptions = $4,650
        // after deductions = $9,600 − $8,750 − $4,650 = −$3,800 → max(0) = $0
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Single", exemptions: 5);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_OneExemption = $219.66; extra = $25.00 → $244.66
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single",
            exemptions: 1, additionalWithholding: 25m);

        Assert.Equal(244.66m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $5,000, pre-tax $500 → taxable wages = $4,500
        // annual = $4,500 × 12 = $54,000; std ded = $8,750 → taxable income = $45,250
        // 0–$3,000 @ 2.00%          = $60.00
        // $3,000–$5,000 @ 3.00%     = $60.00
        // $5,000–$17,000 @ 5.00%    = $600.00
        // $17,000–$45,250 @ 5.75%   = $28,250 × 0.0575 = $1,624.375
        // total = $2,344.375
        // per period = $2,344.375 / 12 = $195.3645... → $195.36
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(4_500m, result.TaxableWages);
        Assert.Equal(195.36m, result.Withholding);
    }

    // ── Low income / zero wages ───────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_WagesLowerThanStandardDeduction_ReturnsZeroWithholding()
    {
        // annual = $600 × 12 = $7,200; std ded = $8,750 → negative after deduction → $0
        var result = Calculate(GrossWages: 600m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Not in generic configs assertion ─────────────────────────────

    [Fact]
    public void Virginia_NotInGenericPercentageMethodConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.VA),
            "VA should not be in StateTaxConfigs2026 — it uses VirginiaWithholdingCalculator.");
    }

    // ── Explanation ───────────────────────────────────────────────────

    [Fact]
    public void Explanation_Single_ShowsDeductionBracketsAndDeannualization()
    {
        // $2,000 biweekly Single, 0 exemptions:
        // annualize = 52,000; std ded = 8,750; annual taxable = 43,250
        // tax = 60 + 60 + 600 + (26,250 × 5.75%) = 2,229.375
        // per period = 2,229.375 / 26 = 85.745... → 85.75
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single");

        Assert.NotNull(result.WithholdingSteps);
        var annualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annualize wages"));
        Assert.Equal(52_000m, annualizeStep.Value);
        var dedStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Less standard deduction"));
        Assert.Equal(8_750m, dedStep.Value);
        var taxableStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual taxable income");
        Assert.Equal(43_250m, taxableStep.Value);
        var bracketStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual tax from Virginia's graduated brackets");
        Assert.Equal(2_229.375m, bracketStep.Value);
        // Top dollar falls in the 5.75% bracket over $17,000
        Assert.Contains("5.75", bracketStep.Detail);
        Assert.Contains("$17,000", bracketStep.Detail);
        var deannualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "De-annualize to this pay period");
        Assert.Equal(85.75m, deannualizeStep.Value);
        Assert.Contains("93045", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_Exemptions_EmitPersonalExemptionStep()
    {
        // 2 exemptions × $930 = $1,860 annual deduction
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single", exemptions: 2);

        var exemptionStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Less personal exemptions (VA-4)");
        Assert.Equal(1_860m, exemptionStep.Value);
    }

    [Fact]
    public void Explanation_WagesBelowStandardDeduction_OmitsBracketStep()
    {
        // annual = $600 × 12 = $7,200 < std ded $8,750 → zero taxable, no bracket step
        var result = Calculate(GrossWages: 600m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
        var taxableStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual taxable income");
        Assert.Equal(0m, taxableStep.Value);
        Assert.DoesNotContain(result.WithholdingSteps!, s => s.Label == "Annual tax from Virginia's graduated brackets");
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        int exemptions = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.VA,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["Exemptions"] = exemptions,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
