using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Maine;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Maine (ME) state income tax withholding.
/// Maine uses the dedicated <see cref="MaineWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from Maine Revenue Services'
/// annualized percentage-method formula (2026 Withholding Tables):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − (allowances × $5,300))
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Maine parameters:
///   Standard deduction: $15,300 (Single) / $30,600 (Married)
///   Per-allowance deduction: $5,300
///   Single brackets:  5.8% on $0–$27,400 | 6.75% on $27,401–$64,850 | 7.15% over $64,850
///   Married brackets: 5.8% on $0–$54,850 | 6.75% on $54,851–$129,750 | 7.15% over $129,750
/// </summary>
public class MaineWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMaine()
    {
        var calc = new MaineWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.ME, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new MaineWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsSingleMarried()
    {
        var calc = new MaineWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
    }

    // ── Single filer — first bracket only ───────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracketOnly()
    {
        // annual = $1,500 × 26 = $39,000
        // less std ded (single) = $15,300 → taxable = $23,700
        // $23,700 < $27,400, so entirely in first bracket
        // tax = $23,700 × 5.8% = $1,374.60
        // per period = $1,374.60 / 26 = $52.869... → $52.87
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(52.87m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ───────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $5,000 × 12 = $60,000
        // less std ded (single) = $15,300 → taxable = $44,700
        // $0–$27,400 @ 5.8%  = $1,589.20
        // $27,400–$44,700 @ 6.75% = $17,300 × 0.0675 = $1,167.75
        // total = $2,756.95
        // per period = $2,756.95 / 12 = $229.745... → $229.75
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(229.75m, result.Withholding);
    }

    // ── Single filer — top bracket ───────────────────────────────────

    [Fact]
    public void Single_Biweekly_TopBracket()
    {
        // annual = $4,000 × 26 = $104,000
        // less std ded (single) = $15,300 → taxable = $88,700
        // $0–$27,400 @ 5.8%   = $1,589.20
        // $27,400–$64,850 @ 6.75% = $37,450 × 0.0675 = $2,527.875
        // $64,850–$88,700 @ 7.15% = $23,850 × 0.0715 = $1,705.275
        // total = $5,822.35
        // per period = $5,822.35 / 26 = $223.936... → $223.94
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(223.94m, result.Withholding);
    }

    // ── Married filer — first bracket only ───────────────────────────

    [Fact]
    public void Married_Monthly_FirstBracketOnly()
    {
        // annual = $6,000 × 12 = $72,000
        // less std ded (married) = $30,600 → taxable = $41,400
        // $41,400 < $54,850, entirely in first bracket
        // tax = $41,400 × 5.8% = $2,401.20
        // per period = $2,401.20 / 12 = $200.10
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(200.10m, result.Withholding);
    }

    // ── Married filer — crosses into second bracket ───────────────────

    [Fact]
    public void Married_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $8,000 × 12 = $96,000
        // less std ded (married) = $30,600 → taxable = $65,400
        // $0–$54,850 @ 5.8%   = $3,181.30
        // $54,850–$65,400 @ 6.75% = $10,550 × 0.0675 = $712.125
        // total = $3,893.425
        // per period = $3,893.425 / 12 = $324.452... → $324.45
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(324.45m, result.Withholding);
    }

    // ── Married filer — top bracket ───────────────────────────────────

    [Fact]
    public void Married_Monthly_TopBracket()
    {
        // annual = $15,000 × 12 = $180,000
        // less std ded (married) = $30,600 → taxable = $149,400
        // $0–$54,850 @ 5.8%     = $3,181.30
        // $54,850–$129,750 @ 6.75% = $74,900 × 0.0675 = $5,055.75
        // $129,750–$149,400 @ 7.15% = $19,650 × 0.0715 = $1,404.975
        // total = $9,642.025
        // per period = $9,642.025 / 12 = $803.502... → $803.50
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(803.50m, result.Withholding);
    }

    // ── Allowances ───────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // less std ded (single) = $15,300
        // less 1 allowance     = $5,300
        // taxable = $57,400
        // $0–$27,400 @ 5.8%   = $1,589.20
        // $27,400–$57,400 @ 6.75% = $30,000 × 0.0675 = $2,025.00
        // total = $3,614.20
        // per period = $3,614.20 / 26 = $139.007... → $139.01
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(139.01m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Biweekly_FirstBracketOnly = $52.87; extra = $30.00 → $82.87
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single", additionalWithholding: 30m);

        Assert.Equal(82.87m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // less std ded (single) = $15,300 → annual taxable = $49,700
        // $0–$27,400 @ 5.8%   = $1,589.20
        // $27,400–$49,700 @ 6.75% = $22,300 × 0.0675 = $1,505.25
        // total = $3,094.45
        // per period = $3,094.45 / 26 = $119.017... → $119.02
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(119.02m, result.Withholding);
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
        var calc = new MaineWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.ME,
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

