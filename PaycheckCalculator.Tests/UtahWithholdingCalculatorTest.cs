using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Core.Tax.Utah;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Utah (UT) state income tax withholding.
/// Utah uses the dedicated <see cref="UtahWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Utah State Tax Commission
/// Publication 14 annualized formula (2026):
///
///   annual wages   = per-period taxable wages × pay periods
///   annual tax     = annual wages × 4.5%
///   gross credit   = allowances × base credit per allowance
///                    (Single $450 / Married $900)
///   phase-out amt  = max(0, annual wages − threshold) × 1.3%
///                    (threshold: Single $9,107 / Married $18,213)
///   net credit     = max(0, gross credit − phase-out amount)
///   annual w/h     = max(0, annual tax − net credit)
///   per-period w/h = round(annual w/h ÷ periods, 2) + extra
///
/// 2026 Utah parameters:
///   Flat rate: 4.5%
///   Allowance base credit:    Single $450 / Married $900 (per allowance)
///   Phase-out threshold:      Single $9,107 / Married $18,213
///   Phase-out rate:           1.3%
///
/// Sources:
///   • Utah State Tax Commission, Publication 14: Withholding Tax Guide (2026).
/// </summary>
public class UtahWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsUtah()
    {
        var calc = new UtahWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.UT, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsAreSingleAndMarried()
    {
        var calc  = new UtahWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains("Single",  field.Options);
        Assert.Contains("Married", field.Options);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSingle_ReturnsNoErrors()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var values = BuildValues("Single", 0, 0m);

        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_ValidMarried_ReturnsNoErrors()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var values = BuildValues("Married", 2, 10m);

        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var values = BuildValues("HeadOfHousehold", 0, 0m);
        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var values = BuildValues("Single", -1, 0m);
        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Allowances", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc   = new UtahWithholdingCalculator(TestSchemas.Provider);
        var values = BuildValues("Single", 0, -5m);
        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    // ── Single filer — no allowances ────────────────────────────────

    [Fact]
    public void Single_Biweekly_NoAllowances()
    {
        // annual = $1,500 × 26 = $39,000
        // annual tax = $39,000 × 4.5% = $1,755
        // credit = 0
        // per period = $1,755 / 26 = $67.50
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single", allowances: 0);

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(67.50m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_NoAllowances()
    {
        // annual = $4,000 × 12 = $48,000
        // annual tax = $48,000 × 4.5% = $2,160
        // per period = $2,160 / 12 = $180.00
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Single", allowances: 0);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(180.00m, result.Withholding);
    }

    [Fact]
    public void Single_Weekly_NoAllowances()
    {
        // annual = $750 × 52 = $39,000
        // annual tax = $39,000 × 4.5% = $1,755
        // per period = $1,755 / 52 = $33.75
        var result = Calculate(GrossWages: 750m, PayFrequency.Weekly, "Single", allowances: 0);

        Assert.Equal(750m, result.TaxableWages);
        Assert.Equal(33.75m, result.Withholding);
    }

    // ── Single filer — allowances with phase-out ────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_AbovePhaseOutThreshold()
    {
        // annual = $1,500 × 26 = $39,000
        // annual tax = $39,000 × 4.5% = $1,755
        // gross credit = 1 × $450 = $450
        // excess = max(0, $39,000 − $9,107) = $29,893
        // phase-out = $29,893 × 1.3% = $388.609
        // net credit = max(0, $450 − $388.609) = $61.391
        // annual w/h = max(0, $1,755 − $61.391) = $1,693.609
        // per period = round($1,693.609 / 26, 2) = round($65.1388...) = $65.14
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(65.14m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_OneAllowance_BelowPhaseOutThreshold()
    {
        // annual = $300 × 26 = $7,800
        // annual tax = $7,800 × 4.5% = $351
        // gross credit = 1 × $450 = $450
        // excess = max(0, $7,800 − $9,107) = $0  (below threshold)
        // phase-out = $0
        // net credit = max(0, $450 − $0) = $450
        // annual w/h = max(0, $351 − $450) = $0  (credit exceeds tax)
        var result = Calculate(GrossWages: 300m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_OneAllowance_PartialPhaseOut()
    {
        // annual = $400 × 26 = $10,400
        // annual tax = $10,400 × 4.5% = $468
        // gross credit = 1 × $450 = $450
        // excess = max(0, $10,400 − $9,107) = $1,293
        // phase-out = $1,293 × 1.3% = $16.809
        // net credit = max(0, $450 − $16.809) = $433.191
        // annual w/h = max(0, $468 − $433.191) = $34.809
        // per period = round($34.809 / 26, 2) = round($1.3388...) = $1.34
        var result = Calculate(GrossWages: 400m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(400m, result.TaxableWages);
        Assert.Equal(1.34m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_HighIncome_AllowancePhasedOutFully()
    {
        // annual = $3,000 × 26 = $78,000
        // annual tax = $78,000 × 4.5% = $3,510
        // gross credit = 1 × $450 = $450
        // excess = max(0, $78,000 − $9,107) = $68,893
        // phase-out = $68,893 × 1.3% = $895.609
        // net credit = max(0, $450 − $895.609) = $0  (fully phased out)
        // annual w/h = $3,510
        // per period = $3,510 / 26 = $135.00
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(135.00m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_TwoAllowances_PartialPhaseOut()
    {
        // annual = $3,500 × 12 = $42,000
        // annual tax = $42,000 × 4.5% = $1,890
        // gross credit = 2 × $450 = $900
        // excess = max(0, $42,000 − $9,107) = $32,893
        // phase-out = $32,893 × 1.3% = $427.609
        // net credit = max(0, $900 − $427.609) = $472.391
        // annual w/h = max(0, $1,890 − $472.391) = $1,417.609
        // per period = round($1,417.609 / 12, 2) = round($118.134...) = $118.13
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Monthly, "Single", allowances: 2);

        Assert.Equal(3_500m, result.TaxableWages);
        Assert.Equal(118.13m, result.Withholding);
    }

    // ── Married filer — no allowances ───────────────────────────────

    [Fact]
    public void Married_Biweekly_NoAllowances()
    {
        // annual = $2,000 × 26 = $52,000
        // annual tax = $52,000 × 4.5% = $2,340
        // per period = $2,340 / 26 = $90.00
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Married", allowances: 0);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(90.00m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_NoAllowances()
    {
        // annual = $5,000 × 12 = $60,000
        // annual tax = $60,000 × 4.5% = $2,700
        // per period = $2,700 / 12 = $225.00
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married", allowances: 0);

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(225.00m, result.Withholding);
    }

    // ── Married filer — allowances with phase-out ────────────────────

    [Fact]
    public void Married_Biweekly_TwoAllowances_AbovePhaseOutThreshold()
    {
        // annual = $2,000 × 26 = $52,000
        // annual tax = $52,000 × 4.5% = $2,340
        // gross credit = 2 × $900 = $1,800
        // excess = max(0, $52,000 − $18,213) = $33,787
        // phase-out = $33,787 × 1.3% = $439.231
        // net credit = max(0, $1,800 − $439.231) = $1,360.769
        // annual w/h = max(0, $2,340 − $1,360.769) = $979.231
        // per period = round($979.231 / 26, 2) = round($37.6627...) = $37.66
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Married", allowances: 2);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(37.66m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_ThreeAllowances_PartialPhaseOut()
    {
        // annual = $5,000 × 12 = $60,000
        // annual tax = $60,000 × 4.5% = $2,700
        // gross credit = 3 × $900 = $2,700
        // excess = max(0, $60,000 − $18,213) = $41,787
        // phase-out = $41,787 × 1.3% = $543.231
        // net credit = max(0, $2,700 − $543.231) = $2,156.769
        // annual w/h = max(0, $2,700 − $2,156.769) = $543.231
        // per period = round($543.231 / 12, 2) = round($45.269...) = $45.27
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married", allowances: 3);

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(45.27m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TwoAllowances_BelowPhaseOutThreshold()
    {
        // annual = $600 × 26 = $15,600
        // annual tax = $15,600 × 4.5% = $702
        // gross credit = 2 × $900 = $1,800
        // excess = max(0, $15,600 − $18,213) = $0  (below threshold)
        // phase-out = $0
        // net credit = $1,800
        // annual w/h = max(0, $702 − $1,800) = $0  (credit exceeds tax)
        var result = Calculate(GrossWages: 600m, PayFrequency.Biweekly, "Married", allowances: 2);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Extra withholding ────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_NoAllowances_WithExtraWithholding()
    {
        // annual = $1,000 × 26 = $26,000
        // annual tax = $26,000 × 4.5% = $1,170
        // per period base = $1,170 / 26 = $45.00
        // + $50 extra = $95.00
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            allowances: 0, extraWithholding: 50m);

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(95.00m, result.Withholding);
    }

    // ── Pre-tax deductions ───────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_PreTaxDeductionReducesTaxableWages()
    {
        // per-period taxable = $2,000 − $200 = $1,800
        // annual = $1,800 × 26 = $46,800
        // annual tax = $46,800 × 4.5% = $2,106
        // per period = $2,106 / 26 = $81.00
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single",
            allowances: 0, preTaxDeductions: 200m);

        Assert.Equal(1_800m, result.TaxableWages);
        Assert.Equal(81.00m, result.Withholding);
    }

    // ── Zero / near-zero wages ───────────────────────────────────────

    [Fact]
    public void Single_ZeroWages_ReturnsZero()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single", allowances: 0);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_PreTaxDeductionExceedsWages_ReturnsZero()
    {
        // per-period taxable = max(0, $500 − $600) = $0
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly, "Single",
            allowances: 0, preTaxDeductions: 600m);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Semimonthly / other frequencies ─────────────────────────────

    [Fact]
    public void Married_Semimonthly_NoAllowances()
    {
        // annual = $2,500 × 24 = $60,000
        // annual tax = $60,000 × 4.5% = $2,700
        // per period = $2,700 / 24 = $112.50
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Semimonthly, "Married", allowances: 0);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(112.50m, result.Withholding);
    }

    // ── Helper methods ───────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency frequency,
        string filingStatus,
        int allowances = 0,
        decimal extraWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc    = new UtahWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.UT,
            GrossWages: GrossWages,
            PayPeriod: frequency,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = BuildValues(filingStatus, allowances, extraWithholding);

        return calc.Calculate(context, values);
    }

    private static StateInputValues BuildValues(string filingStatus, int allowances, decimal extraWithholding)
    {
        return new StateInputValues
        {
            ["FilingStatus"]          = filingStatus,
            ["Allowances"]            = allowances,
            ["AdditionalWithholding"] = extraWithholding
        };
    }
}
