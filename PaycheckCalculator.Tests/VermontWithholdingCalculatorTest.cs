using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Vermont;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Vermont (VT) state income tax withholding.
/// Vermont uses the dedicated <see cref="VermontWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Vermont Department of Taxes
/// annualized percentage-method formula (BP-55, 2026 Income Tax Withholding
/// Instructions, Tables, and Charts):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − (allowances × $5,400))
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// Vermont has no state standard deduction for withholding purposes;
/// allowances ($5,400 each) are the only annualized offset.
///
/// 2026 Vermont parameters:
///   Per-allowance deduction: $5,400
///   Single brackets:  3.35% on $0–$47,900 | 6.60% on $47,900–$116,000 |
///                     7.60% on $116,000–$242,000 | 8.75% over $242,000
///   Married brackets: 3.35% on $0–$79,950 | 6.60% on $79,950–$193,300 |
///                     7.60% on $193,300–$294,600 | 8.75% over $294,600
///   HoH brackets:     3.35% on $0–$64,200 | 6.60% on $64,200–$165,700 |
///                     7.60% on $165,700–$268,300 | 8.75% over $268,300
/// </summary>
public class VermontWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsVermont()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.VT, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidFilingStatus_ReturnsNoErrors()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        });
        Assert.Contains(errors, e => e.Contains("Additional Withholding"));
    }

    // ── Single filer — first bracket only ───────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracketOnly()
    {
        // annual = $1,000 × 26 = $26,000 (no allowances)
        // taxable = $26,000 (entirely in first bracket $0–$47,900)
        // tax = $26,000 × 3.35% = $871.00
        // per period = $871.00 / 26 = $33.50
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(33.50m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ───────────────────

    [Fact]
    public void Single_Biweekly_CrossesIntoSecondBracket()
    {
        // annual = $2,000 × 26 = $52,000 (no allowances)
        // taxable = $52,000
        // $0–$47,900 @ 3.35%  = $47,900 × 0.0335 = $1,604.65
        // $47,900–$52,000 @ 6.60% = $4,100 × 0.066 = $270.60
        // total = $1,875.25
        // per period = $1,875.25 / 26 = $72.1250 → $72.13
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(72.13m, result.Withholding);
    }

    // ── Single filer — third bracket ─────────────────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoThirdBracket()
    {
        // annual = $12,000 × 12 = $144,000 (no allowances)
        // $0–$47,900 @ 3.35%       = $1,604.65
        // $47,900–$116,000 @ 6.60% = $68,100 × 0.066 = $4,494.60
        // $116,000–$144,000 @ 7.60% = $28,000 × 0.076 = $2,128.00
        // total = $8,227.25
        // per period = $8,227.25 / 12 = $685.604166... → $685.60
        var result = Calculate(GrossWages: 12_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(685.60m, result.Withholding);
    }

    // ── Single filer — top bracket ───────────────────────────────────

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $25,000 × 12 = $300,000 (no allowances)
        // $0–$47,900 @ 3.35%       = $1,604.65
        // $47,900–$116,000 @ 6.60% = $68,100 × 0.066 = $4,494.60
        // $116,000–$242,000 @ 7.60% = $126,000 × 0.076 = $9,576.00
        // $242,000–$300,000 @ 8.75% = $58,000 × 0.0875 = $5,075.00
        // total = $20,750.25
        // per period = $20,750.25 / 12 = $1,729.1875 → $1,729.19
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_729.19m, result.Withholding);
    }

    // ── Married filer — first bracket only ───────────────────────────

    [Fact]
    public void Married_Monthly_FirstBracketOnly()
    {
        // annual = $5,000 × 12 = $60,000 (no allowances)
        // taxable = $60,000 (entirely in first bracket $0–$79,950)
        // tax = $60,000 × 3.35% = $2,010.00
        // per period = $2,010.00 / 12 = $167.50
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(167.50m, result.Withholding);
    }

    // ── Married filer — second bracket ───────────────────────────────

    [Fact]
    public void Married_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $15,000 × 12 = $180,000 (no allowances)
        // $0–$79,950 @ 3.35%       = $79,950 × 0.0335 = $2,678.325
        // $79,950–$180,000 @ 6.60% = $100,050 × 0.066 = $6,603.30
        // total = $9,281.625
        // per period = $9,281.625 / 12 = $773.46875 → $773.47
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(773.47m, result.Withholding);
    }

    // ── Married filer — top bracket ───────────────────────────────────

    [Fact]
    public void Married_Monthly_TopBracket()
    {
        // annual = $30,000 × 12 = $360,000 (no allowances)
        // $0–$79,950 @ 3.35%         = $79,950 × 0.0335 = $2,678.325
        // $79,950–$193,300 @ 6.60%   = $113,350 × 0.066 = $7,481.10
        // $193,300–$294,600 @ 7.60%  = $101,300 × 0.076 = $7,698.80
        // $294,600–$360,000 @ 8.75%  = $65,400 × 0.0875 = $5,722.50
        // total = $23,580.725
        // per period = $23,580.725 / 12 = $1,965.060416... → $1,965.06
        var result = Calculate(GrossWages: 30_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(1_965.06m, result.Withholding);
    }

    // ── Head of Household filer ───────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_FirstBracketOnly()
    {
        // annual = $1,500 × 26 = $39,000 (no allowances)
        // taxable = $39,000 (entirely in first HoH bracket $0–$64,200)
        // tax = $39,000 × 3.35% = $1,306.50
        // per period = $1,306.50 / 26 = $50.25
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(50.25m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_CrossesIntoSecondBracket()
    {
        // annual = $3,500 × 26 = $91,000 (no allowances)
        // $0–$64,200 @ 3.35%       = $64,200 × 0.0335 = $2,150.70
        // $64,200–$91,000 @ 6.60%  = $26,800 × 0.066 = $1,768.80
        // total = $3,919.50
        // per period = $3,919.50 / 26 = $150.75 (exact)
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(150.75m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_TopBracket()
    {
        // annual = $30,000 × 12 = $360,000 (no allowances)
        // $0–$64,200 @ 3.35%         = $64,200 × 0.0335 = $2,150.70
        // $64,200–$165,700 @ 6.60%   = $101,500 × 0.066 = $6,699.00
        // $165,700–$268,300 @ 7.60%  = $102,600 × 0.076 = $7,797.60
        // $268,300–$360,000 @ 8.75%  = $91,700 × 0.0875 = $8,023.75
        // total = $24,671.05
        // per period = $24,671.05 / 12 = $2,055.920833... → $2,055.92
        var result = Calculate(GrossWages: 30_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(2_055.92m, result.Withholding);
    }

    // ── Allowances ───────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // less 2 allowances = $78,000 − 2 × $5,400 = $78,000 − $10,800 = $67,200
        // $0–$47,900 @ 3.35%       = $47,900 × 0.0335 = $1,604.65
        // $47,900–$67,200 @ 6.60%  = $19,300 × 0.066 = $1,273.80
        // total = $2,878.45
        // per period = $2,878.45 / 26 = $110.7096... → $110.71
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(110.71m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_OneAllowance_ReducesTax()
    {
        // annual = $4,000 × 26 = $104,000
        // less 1 allowance = $104,000 − $5,400 = $98,600
        // $0–$79,950 @ 3.35%       = $79,950 × 0.0335 = $2,678.325
        // $79,950–$98,600 @ 6.60%  = $18,650 × 0.066 = $1,230.90
        // total = $3,909.225
        // per period = $3,909.225 / 26 = $150.3548... → $150.35
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Married", allowances: 1);

        Assert.Equal(150.35m, result.Withholding);
    }

    [Fact]
    public void Allowances_HighEnoughToZeroOutTax()
    {
        // 20 allowances = $108,000 annual deduction
        // annual wages = $1,000 × 26 = $26,000; $26,000 − $108,000 = max(0) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 20);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Biweekly_FirstBracketOnly = $33.50; extra = $15.00 → $48.50
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 15m);

        Assert.Equal(48.50m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $2,000, pre-tax $300 → taxable wages = $1,700
        // annual = $1,700 × 26 = $44,200
        // tax = $44,200 × 3.35% = $1,480.70 (entirely in first bracket)
        // per period = $1,480.70 / 26 = $56.95 (exact)
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 300m);

        Assert.Equal(1_700m, result.TaxableWages);
        Assert.Equal(56.95m, result.Withholding);
    }

    // ── Low income / zero wages ───────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_LowIncome_NoTaxWhenAllCoveredByAllowances()
    {
        // annual = $800 × 26 = $20,800; 1 allowance = $5,400 → taxable = $15,400
        // tax = $15,400 × 3.35% = $515.90
        // per period = $515.90 / 26 = $19.843... → $19.84
        var result = Calculate(GrossWages: 800m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(19.84m, result.Withholding);
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        int allowances = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new VermontWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.VT,
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
