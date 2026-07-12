using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Minnesota;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Minnesota (MN) state income tax withholding.
/// Minnesota uses the dedicated <see cref="MinnesotaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Minnesota Department of
/// Revenue annualized percentage-method formula (2026, Pub. 89):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − (allowances × $5,300))
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Minnesota parameters:
///   Standard deduction: $15,300 (Single) / $30,600 (Married) / $23,000 (Head of Household)
///   Per-allowance deduction: $5,300
///   Single brackets:  5.35% on $0–$33,310 | 6.80% on $33,310–$109,430 |
///                     7.85% on $109,430–$203,150 | 9.85% over $203,150
///   Married brackets: 5.35% on $0–$48,700 | 6.80% on $48,700–$193,480 |
///                     7.85% on $193,480–$337,930 | 9.85% over $337,930
///   HoH brackets:     5.35% on $0–$41,010 | 6.80% on $41,010–$164,800 |
///                     7.85% on $164,800–$270,060 | 9.85% over $270,060
/// </summary>
public class MinnesotaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMinnesota()
    {
        var calc = new MinnesotaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.MN, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new MinnesotaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new MinnesotaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer — first bracket only ───────────────────────────

    [Fact]
    public void Single_Monthly_FirstBracketOnly()
    {
        // annual = $1,500 × 12 = $18,000
        // less std ded (single) = $15,300 → taxable = $2,700
        // $2,700 < $33,310, so entirely in first bracket
        // tax = $2,700 × 5.35% = $144.45
        // per period = $144.45 / 12 = $12.0375 → $12.04
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(12.04m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ───────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $5,000 × 12 = $60,000
        // less std ded (single) = $15,300 → taxable = $44,700
        // $33,310 × 5.35% = $1,782.085
        // ($44,700 − $33,310) × 6.80% = $11,390 × 0.068 = $774.52
        // total = $2,556.605
        // per period = $2,556.605 / 12 = $213.050416... → $213.05
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(213.05m, result.Withholding);
    }

    // ── Single filer — third bracket ────────────────────────────────

    [Fact]
    public void Single_Biweekly_ThirdBracket()
    {
        // annual = $6,000 × 26 = $156,000
        // less std ded (single) = $15,300 → taxable = $140,700
        // $33,310 × 5.35% = $1,782.085
        // ($109,430 − $33,310) × 6.80% = $76,120 × 0.068 = $5,176.16
        // ($140,700 − $109,430) × 7.85% = $31,270 × 0.0785 = $2,454.695
        // total = $9,412.94
        // per period = $9,412.94 / 26 = $362.036923... → $362.04
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(362.04m, result.Withholding);
    }

    // ── Single filer — top bracket ───────────────────────────────────

    [Fact]
    public void Single_Biweekly_TopBracket()
    {
        // annual = $10,000 × 26 = $260,000
        // less std ded (single) = $15,300 → taxable = $244,700
        // $33,310 × 5.35%                    = $1,782.085
        // ($109,430 − $33,310) × 6.80%       = $76,120 × 0.068 = $5,176.16
        // ($203,150 − $109,430) × 7.85%      = $93,720 × 0.0785 = $7,357.02
        // ($244,700 − $203,150) × 9.85%      = $41,550 × 0.0985 = $4,092.675
        // total = $18,407.94
        // per period = $18,407.94 / 26 = $707.9976923... → $708.00
        var result = Calculate(GrossWages: 10_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(708.00m, result.Withholding);
    }

    // ── Married filer — first bracket only ───────────────────────────

    [Fact]
    public void Married_Monthly_FirstBracketOnly()
    {
        // annual = $5,000 × 12 = $60,000
        // less std ded (married) = $30,600 → taxable = $29,400
        // $29,400 < $48,700, entirely in first bracket
        // tax = $29,400 × 5.35% = $1,572.90
        // per period = $1,572.90 / 12 = $131.075 → $131.08
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(131.08m, result.Withholding);
    }

    // ── Married filer — crosses into second bracket ───────────────────

    [Fact]
    public void Married_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $8,000 × 12 = $96,000
        // less std ded (married) = $30,600 → taxable = $65,400
        // $48,700 × 5.35% = $2,605.45
        // ($65,400 − $48,700) × 6.80% = $16,700 × 0.068 = $1,135.60
        // total = $3,741.05
        // per period = $3,741.05 / 12 = $311.754166... → $311.75
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(311.75m, result.Withholding);
    }

    // ── Married filer — top bracket ───────────────────────────────────

    [Fact]
    public void Married_Biweekly_TopBracket()
    {
        // annual = $15,000 × 26 = $390,000
        // less std ded (married) = $30,600 → taxable = $359,400
        // $48,700 × 5.35%                     = $2,605.45
        // ($193,480 − $48,700) × 6.80%        = $144,780 × 0.068 = $9,845.04
        // ($337,930 − $193,480) × 7.85%       = $144,450 × 0.0785 = $11,339.325
        // ($359,400 − $337,930) × 9.85%       = $21,470 × 0.0985 = $2,114.795
        // total = $25,904.61
        // per period = $25,904.61 / 26 = $996.331153... → $996.33
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(996.33m, result.Withholding);
    }

    // ── Head of Household — first bracket only ────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_FirstBracketOnly()
    {
        // annual = $4,000 × 12 = $48,000
        // less std ded (HoH) = $23,000 → taxable = $25,000
        // $25,000 < $41,010, entirely in first bracket
        // tax = $25,000 × 5.35% = $1,337.50
        // per period = $1,337.50 / 12 = $111.458333... → $111.46
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(111.46m, result.Withholding);
    }

    // ── Head of Household — crosses into second bracket ──────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_CrossesIntoSecondBracket()
    {
        // annual = $3,500 × 26 = $91,000
        // less std ded (HoH) = $23,000 → taxable = $68,000
        // $41,010 × 5.35% = $2,194.035
        // ($68,000 − $41,010) × 6.80% = $26,990 × 0.068 = $1,835.32
        // total = $4,029.355
        // per period = $4,029.355 / 26 = $154.9751923... → $154.98
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(154.98m, result.Withholding);
    }

    // ── Head of Household — top bracket ──────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_TopBracket()
    {
        // annual = $12,000 × 26 = $312,000
        // less std ded (HoH) = $23,000 → taxable = $289,000
        // $41,010 × 5.35%                     = $2,194.035
        // ($164,800 − $41,010) × 6.80%        = $123,790 × 0.068 = $8,417.72
        // ($270,060 − $164,800) × 7.85%       = $105,260 × 0.0785 = $8,262.91
        // ($289,000 − $270,060) × 9.85%       = $18,940 × 0.0985 = $1,865.59
        // total = $20,740.255
        // per period = $20,740.255 / 26 = $797.702115... → $797.70
        var result = Calculate(GrossWages: 12_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(797.70m, result.Withholding);
    }

    // ── Allowances ───────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // less std ded (single) = $15,300
        // less 1 allowance     = $5,300
        // taxable = $57,400
        // $33,310 × 5.35% = $1,782.085
        // ($57,400 − $33,310) × 6.80% = $24,090 × 0.068 = $1,638.12
        // total = $3,420.205
        // per period = $3,420.205 / 26 = $131.546346... → $131.55
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(131.55m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from HeadOfHousehold_Biweekly_CrossesIntoSecondBracket = $154.98; extra = $25.00 → $179.98
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Biweekly, "Head of Household",
            additionalWithholding: 25m);

        Assert.Equal(179.98m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // less std ded (single) = $15,300 → annual taxable = $49,700
        // $33,310 × 5.35% = $1,782.085
        // ($49,700 − $33,310) × 6.80% = $16,390 × 0.068 = $1,114.52
        // total = $2,896.605
        // per period = $2,896.605 / 26 = $111.408 → $111.41
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(111.41m, result.Withholding);
    }

    // ── Low income (below standard deduction) ────────────────────────

    [Fact]
    public void Single_Biweekly_IncomeBelowStandardDeduction_ReturnsZero()
    {
        // annual = $400 × 26 = $10,400
        // less std ded (single) = $15,300 → taxable = max(0, -$4,900) = $0
        var result = Calculate(GrossWages: 400m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_IncomeBelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,500 × 12 = $18,000
        // less std ded (HoH) = $23,000 → taxable = max(0, -$5,000) = $0
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
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
        var calc = new MinnesotaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.MN,
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
