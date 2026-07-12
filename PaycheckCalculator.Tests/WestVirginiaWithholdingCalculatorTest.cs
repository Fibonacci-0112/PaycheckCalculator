using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.WestVirginia;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for West Virginia (WV) state income tax withholding.
/// West Virginia uses the dedicated <see cref="WestVirginiaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the West Virginia State Tax
/// Department Employee's Withholding Exemption Certificate (Form IT-104, 2026)
/// and WV Code § 11-21-71.
///
/// Algorithm:
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − (exemptions × $2,000))
///   annual tax     = graduated brackets applied to annual taxable income
///                    (same thresholds for all filing statuses):
///                    3.00% on $0–$10,000     | 4.00% on $10,001–$25,000 |
///                    4.50% on $25,001–$40,000 | 6.00% on $40,001–$60,000 |
///                    6.50% over $60,000
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 West Virginia parameters (Form IT-104):
///   Standard deduction: none (no state standard deduction in the withholding formula)
///   Per-exemption deduction: $2,000 (Form IT-104)
///   Bracket thresholds are the same for all filing statuses.
/// </summary>
public class WestVirginiaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsWestVirginia()
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.WV, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Exemptions_AdditionalWithholding()
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_HasSingleAndMarried()
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Married")]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = status });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Head of Household" });
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
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
        // annual = $800 × 12 = $9,600; no std deduction; 0 exemptions → taxable = $9,600
        // $9,600 entirely in first bracket 0–$10,000 @ 3.00% = $288.00
        // per period = $288.00 / 12 = $24.00
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Single");

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(24.00m, result.Withholding);
    }

    // ── Single filer — crosses into second bracket ────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoSecondBracket()
    {
        // annual = $1,000 × 12 = $12,000; taxable = $12,000
        // 0–$10,000 @ 3.00%        = $300.00
        // $10,000–$12,000 @ 4.00%  = $2,000 × 0.04 = $80.00
        // total = $380.00
        // per period = $380.00 / 12 = $31.6666... → $31.67
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(31.67m, result.Withholding);
    }

    // ── Single filer — crosses into third bracket ─────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoThirdBracket()
    {
        // annual = $3,000 × 12 = $36,000; taxable = $36,000
        // 0–$10,000 @ 3.00%        = $300.00
        // $10,000–$25,000 @ 4.00%  = $15,000 × 0.04 = $600.00
        // $25,000–$36,000 @ 4.50%  = $11,000 × 0.045 = $495.00
        // total = $1,395.00
        // per period = $1,395.00 / 12 = $116.25
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(116.25m, result.Withholding);
    }

    // ── Single filer — crosses into fourth bracket ────────────────────

    [Fact]
    public void Single_Monthly_CrossesIntoFourthBracket()
    {
        // annual = $4,500 × 12 = $54,000; taxable = $54,000
        // 0–$10,000 @ 3.00%        = $300.00
        // $10,000–$25,000 @ 4.00%  = $15,000 × 0.04 = $600.00
        // $25,000–$40,000 @ 4.50%  = $15,000 × 0.045 = $675.00
        // $40,000–$54,000 @ 6.00%  = $14,000 × 0.06 = $840.00
        // total = $2,415.00
        // per period = $2,415.00 / 12 = $201.25
        var result = Calculate(GrossWages: 4_500m, PayFrequency.Monthly, "Single");

        Assert.Equal(201.25m, result.Withholding);
    }

    // ── Single filer — top bracket (6.5%) ─────────────────────────────

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $6,000 × 12 = $72,000; 0 exemptions → taxable = $72,000
        // 0–$10,000 @ 3.00%       = $300.00
        // $10,000–$25,000 @ 4.00% = $15,000 × 0.04 = $600.00
        // $25,000–$40,000 @ 4.50% = $15,000 × 0.045 = $675.00
        // $40,000–$60,000 @ 6.00% = $20,000 × 0.06 = $1,200.00
        // $60,000–$72,000 @ 6.50% = $12,000 × 0.065 = $780.00
        // total = $3,555.00
        // per period = $3,555.00 / 12 = $296.25
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(6_000m, result.TaxableWages);
        Assert.Equal(296.25m, result.Withholding);
    }

    // ── Married filer — same brackets as Single ───────────────────────

    [Fact]
    public void Married_Monthly_SameBracketsAsSingle()
    {
        // WV uses identical brackets for Single and Married.
        // annual = $1,000 × 12 = $12,000
        // Same calculation as Single_Monthly_CrossesIntoSecondBracket → $31.67
        var resultMarried = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Married");
        var resultSingle = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(resultSingle.Withholding, resultMarried.Withholding);
    }

    [Fact]
    public void Married_Monthly_TopBracket()
    {
        // annual = $6,000 × 12 = $72,000; 0 exemptions → taxable = $72,000
        // Same computation as Single_Monthly_TopBracket → $296.25
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(296.25m, result.Withholding);
    }

    // ── Bracket boundary verification (annual pay period) ────────────

    [Fact]
    public void Single_Annual_ExactlyAt10000BracketBoundary()
    {
        // Annual, $10,000 gross, 0 exemptions:
        // taxable = $10,000 (exactly at first/second bracket boundary)
        // tax = $10,000 × 3.00% = $300.00
        // per period (annual) = $300.00 / 1 = $300.00
        var result = Calculate(GrossWages: 10_000m, PayFrequency.Annual, "Single");

        Assert.Equal(300.00m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt25000BracketBoundary()
    {
        // Annual, $25,000 gross, 0 exemptions:
        // taxable = $25,000 (at second/third bracket boundary)
        // tax = $10,000 × 3.00% + $15,000 × 4.00% = $300 + $600 = $900.00
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Annual, "Single");

        Assert.Equal(900.00m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt40000BracketBoundary()
    {
        // Annual, $40,000 gross, 0 exemptions:
        // taxable = $40,000 (at third/fourth bracket boundary)
        // tax = $300 + $600 + $15,000 × 4.50% = $300 + $600 + $675 = $1,575.00
        var result = Calculate(GrossWages: 40_000m, PayFrequency.Annual, "Single");

        Assert.Equal(1_575.00m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAt60000BracketBoundary()
    {
        // Annual, $60,000 gross, 0 exemptions:
        // taxable = $60,000 (at fourth/top bracket boundary)
        // tax = $300 + $600 + $675 + $20,000 × 6.00% = $300 + $600 + $675 + $1,200 = $2,775.00
        var result = Calculate(GrossWages: 60_000m, PayFrequency.Annual, "Single");

        Assert.Equal(2_775.00m, result.Withholding);
    }

    // ── Exemptions ────────────────────────────────────────────────────

    [Fact]
    public void Single_Monthly_TwoExemptions_ReducesTax()
    {
        // annual = $2,000 × 12 = $24,000; 2 exemptions = $4,000 → taxable = $20,000
        // 0–$10,000 @ 3.00%        = $300.00
        // $10,000–$20,000 @ 4.00%  = $10,000 × 0.04 = $400.00
        // total = $700.00
        // per period = $700.00 / 12 = $58.3333... → $58.33
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single", exemptions: 2);

        Assert.Equal(58.33m, result.Withholding);
    }

    [Fact]
    public void Exemptions_HighEnoughToZeroOutTax()
    {
        // annual = $200 × 12 = $2,400; 2 exemptions = $4,000
        // taxable = max(0, $2,400 − $4,000) = $0 → no withholding
        var result = Calculate(GrossWages: 200m, PayFrequency.Monthly, "Single", exemptions: 2);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_CrossesIntoSecondBracket = $31.67; extra = $10.00 → $41.67
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 10m);

        Assert.Equal(41.67m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual = $800 × 12 = $9,600; 0 exemptions → taxable income = $9,600
        // $9,600 entirely in first bracket @ 3.00% = $288.00
        // per period = $288.00 / 12 = $24.00
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single",
            preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(24.00m, result.Withholding);
    }

    // ── Biweekly pay frequency ────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneExemption_MultiplesBrackets()
    {
        // annual = $1,500 × 26 = $39,000; 1 exemption = $2,000 → taxable = $37,000
        // 0–$10,000 @ 3.00%        = $300.00
        // $10,000–$25,000 @ 4.00%  = $15,000 × 0.04 = $600.00
        // $25,000–$37,000 @ 4.50%  = $12,000 × 0.045 = $540.00
        // total = $1,440.00
        // per period = $1,440.00 / 26 = $55.3846... → $55.38
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single", exemptions: 1);

        Assert.Equal(55.38m, result.Withholding);
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
    public void ZeroTaxableIncome_ReturnsZeroWithholding()
    {
        // $100/month = $1,200 annual; 1 exemption = $2,000 → taxable = $0
        var result = Calculate(GrossWages: 100m, PayFrequency.Monthly, "Single", exemptions: 1);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Not in generic configs assertion ─────────────────────────────

    [Fact]
    public void WestVirginia_NotInGenericPercentageMethodConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.WV),
            "WV should not be in StateTaxConfigs2026 — it uses WestVirginiaWithholdingCalculator.");
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
        var calc = new WestVirginiaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.WV,
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
