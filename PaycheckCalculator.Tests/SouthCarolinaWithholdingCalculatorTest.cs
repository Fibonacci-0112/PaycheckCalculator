using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.SouthCarolina;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for South Carolina (SC) state income tax withholding.
/// South Carolina uses the dedicated <see cref="SouthCarolinaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the SC annualized
/// percentage-method formula (2026) per SCDOR Form WH-1603F:
///   taxable wages  = gross wages − pre-tax deductions (floor $0)
///   annual wages   = taxable wages × pay periods
///   std deduction  = if allowances ≥ 1: min(annual wages × 10%, $7,500); else $0
///   allowance ded  = allowances × $5,000
///   annual taxable = max(0, annual wages − std deduction − allowance deduction)
///   annual tax     = bracket calc: 0% on $0–$3,640; 3% on $3,640–$18,230; 6% on excess
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 South Carolina parameters (SCDOR Form WH-1603F):
///   Standard deduction (allowances ≥ 1): 10% of annualized wages, max $7,500
///   Per-allowance deduction (SC W-4 Line 2): $5,000
///   Brackets: 0% on $0–$3,640 / 3% on $3,640–$18,230 / 6% over $18,230
/// </summary>
public class SouthCarolinaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsSouthCarolina()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.SC, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single — basic withholding ───────────────────────────────────

    [Fact]
    public void Single_Biweekly_TwoAllowances_BasicWithholding()
    {
        // annual wages  = $1,000 × 26 = $26,000
        // std deduction = min($26,000 × 10%, $7,500) = min($2,600, $7,500) = $2,600
        // allowance ded = 2 × $5,000 = $10,000
        // annual taxable = $26,000 − $2,600 − $10,000 = $13,400
        // annual tax     = 3% × ($13,400 − $3,640) = 3% × $9,760 = $292.80
        // per period     = $292.80 / 26 = $11.261538... → $11.26
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(11.26m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_OneAllowance_BasicWithholding()
    {
        // annual wages  = $5,000 × 12 = $60,000
        // std deduction = min($60,000 × 10%, $7,500) = min($6,000, $7,500) = $6,000
        // allowance ded = 1 × $5,000 = $5,000
        // annual taxable = $60,000 − $6,000 − $5,000 = $49,000
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($49,000 − $18,230)
        //                = 3% × $14,590 + 6% × $30,770
        //                = $437.70 + $1,846.20 = $2,283.90
        // per period     = $2,283.90 / 12 = $190.325 → $190.33
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single", allowances: 1);

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(190.33m, result.Withholding);
    }

    // ── Standard deduction: 0 allowances means no std deduction ─────

    [Fact]
    public void Single_Monthly_ZeroAllowances_NoStandardDeduction()
    {
        // annual wages  = $3,000 × 12 = $36,000
        // std deduction = $0 (no allowances claimed)
        // allowance ded = $0
        // annual taxable = $36,000
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($36,000 − $18,230)
        //                = $437.70 + 6% × $17,770 = $437.70 + $1,066.20 = $1,503.90
        // per period     = $1,503.90 / 12 = $125.325 → $125.33
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Single", allowances: 0);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(125.33m, result.Withholding);
    }

    // ── Standard deduction: capped at $7,500 ────────────────────────

    [Fact]
    public void Single_Monthly_HighWages_StandardDeductionCapsAt7500()
    {
        // annual wages  = $8,000 × 12 = $96,000
        // std deduction = min($96,000 × 10%, $7,500) = min($9,600, $7,500) = $7,500 (capped)
        // allowance ded = 1 × $5,000 = $5,000
        // annual taxable = $96,000 − $7,500 − $5,000 = $83,500
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($83,500 − $18,230)
        //                = $437.70 + 6% × $65,270 = $437.70 + $3,916.20 = $4,353.90
        // per period     = $4,353.90 / 12 = $362.825 → $362.83
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Single", allowances: 1);

        Assert.Equal(8_000m, result.TaxableWages);
        Assert.Equal(362.83m, result.Withholding);
    }

    // ── Married ─────────────────────────────────────────────────────

    [Fact]
    public void Married_Monthly_ThreeAllowances_BasicWithholding()
    {
        // SC uses same brackets for Married as Single (no filing-status bracket split).
        // annual wages  = $4,000 × 12 = $48,000
        // std deduction = min($48,000 × 10%, $7,500) = min($4,800, $7,500) = $4,800
        // allowance ded = 3 × $5,000 = $15,000
        // annual taxable = $48,000 − $4,800 − $15,000 = $28,200
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($28,200 − $18,230)
        //                = $437.70 + 6% × $9,970 = $437.70 + $598.20 = $1,035.90
        // per period     = $1,035.90 / 12 = $86.325 → $86.33
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Married", allowances: 3);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(86.33m, result.Withholding);
    }

    // ── Head of Household ────────────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_TwoAllowances_SameBracketsAsSingle()
    {
        // SC uses the same brackets for all filing statuses.
        // annual wages  = $5,000 × 12 = $60,000
        // std deduction = min($60,000 × 10%, $7,500) = $6,000
        // allowance ded = 2 × $5,000 = $10,000
        // annual taxable = $60,000 − $6,000 − $10,000 = $44,000
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($44,000 − $18,230)
        //                = $437.70 + 6% × $25,770 = $437.70 + $1,546.20 = $1,983.90
        // per period     = $1,983.90 / 12 = $165.325 → $165.33
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Head of Household", allowances: 2);

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(165.33m, result.Withholding);
    }

    // ── Bracket boundary tests ───────────────────────────────────────

    [Fact]
    public void Annual_ZeroAllowances_ExactlyAtFirstBracketCeiling_ReturnsZero()
    {
        // annual wages = $3,640; no allowances → std ded = $0; taxable = $3,640
        // All income falls in the 0% band → tax = $0
        var result = Calculate(GrossWages: 3_640m, PayFrequency.Annual, "Single", allowances: 0);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Annual_ZeroAllowances_OneDollarAboveFirstBracket_Taxed3Pct()
    {
        // annual wages = $3,641; no std ded; taxable = $3,641
        // annual tax   = 3% × ($3,641 − $3,640) = 3% × $1 = $0.03
        // per period   = $0.03 / 1 = $0.03
        var result = Calculate(GrossWages: 3_641m, PayFrequency.Annual, "Single", allowances: 0);

        Assert.Equal(0.03m, result.Withholding);
    }

    [Fact]
    public void Annual_ZeroAllowances_ExactlyAtSecondBracketCeiling()
    {
        // annual wages = $18,230; no std ded; taxable = $18,230
        // annual tax   = 3% × ($18,230 − $3,640) = 3% × $14,590 = $437.70
        // per period   = $437.70 / 1 = $437.70
        var result = Calculate(GrossWages: 18_230m, PayFrequency.Annual, "Single", allowances: 0);

        Assert.Equal(437.70m, result.Withholding);
    }

    // ── Allowances eliminate all tax ─────────────────────────────────

    [Fact]
    public void Allowances_EliminateAllTax_ReturnsZero()
    {
        // annual wages  = $10,000 × 1 = $10,000
        // std deduction = min($10,000 × 10%, $7,500) = min($1,000, $7,500) = $1,000
        // allowance ded = 2 × $5,000 = $10,000
        // annual taxable = max(0, $10,000 − $1,000 − $10,000) = $0
        var result = Calculate(GrossWages: 10_000m, PayFrequency.Annual, "Single", allowances: 2);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_OneAllowance_BasicWithholding = $190.33; extra = $25.00 → $215.33
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single",
            allowances: 1, additionalWithholding: 25m);

        Assert.Equal(215.33m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual wages  = $800 × 26 = $20,800
        // std deduction = min($20,800 × 10%, $7,500) = min($2,080, $7,500) = $2,080
        // allowance ded = 2 × $5,000 = $10,000
        // annual taxable = $20,800 − $2,080 − $10,000 = $8,720
        // annual tax     = 3% × ($8,720 − $3,640) = 3% × $5,080 = $152.40
        // per period     = $152.40 / 26 = $5.861538... → $5.86
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            allowances: 2, preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(5.86m, result.Withholding);
    }

    // ── Semimonthly pay frequency ────────────────────────────────────

    [Fact]
    public void Single_Semimonthly_CorrectDeannualization()
    {
        // annual wages  = $2,000 × 24 = $48,000
        // std deduction = min($48,000 × 10%, $7,500) = $4,800
        // allowance ded = 1 × $5,000 = $5,000
        // annual taxable = $48,000 − $4,800 − $5,000 = $38,200
        // annual tax     = 3% × ($18,230 − $3,640) + 6% × ($38,200 − $18,230)
        //                = $437.70 + 6% × $19,970 = $437.70 + $1,198.20 = $1,635.90
        // per period     = $1,635.90 / 24 = $68.1625 → $68.16
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Semimonthly, "Single", allowances: 1);

        Assert.Equal(68.16m, result.Withholding);
    }

    // ── Zero gross wages ─────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Validation ───────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = -1
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Allowances", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
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
        var calc = new SouthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.SC,
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
