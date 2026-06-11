using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.NorthCarolina;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for North Carolina (NC) state income tax withholding.
/// North Carolina uses the dedicated <see cref="NorthCarolinaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the NC annualized
/// percentage-method formula (2026) per NC DOR Publication NC-30:
///   taxable wages  = gross wages − pre-tax deductions (floor $0)
///   annual wages   = taxable wages × pay periods
///   annual taxable = max(0, annual wages − std ded − (allowances × $2,500))
///   annual tax     = annual taxable × 4.5%
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 North Carolina parameters:
///   Standard deduction: $12,750 (Single) / $25,500 (Married) / $19,125 (HoH)
///   Per-allowance deduction (NC-4 Line 2): $2,500
///   Tax rate: 4.5% flat
/// </summary>
public class NorthCarolinaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNorthCarolina()
    {
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.NC, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single ──────────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_BasicWithholding()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $12,750
        // annual taxable = $26,000 − $12,750 = $13,250
        // annual tax = $13,250 × 0.045 = $596.25
        // per period = $596.25 / 26 = $22.932692... → $22.93
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(22.93m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BasicWithholding()
    {
        // annual = $5,000 × 12 = $60,000
        // std ded = $12,750
        // annual taxable = $60,000 − $12,750 = $47,250
        // annual tax = $47,250 × 0.045 = $2,126.25
        // per period = $2,126.25 / 12 = $177.1875 → $177.19
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(177.19m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $800 × 12 = $9,600; std ded = $12,750
        // annual taxable = max(0, $9,600 − $12,750) = $0
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married ─────────────────────────────────────────────────────

    [Fact]
    public void Married_Monthly_BasicWithholding()
    {
        // annual = $6,000 × 12 = $72,000
        // std ded = $25,500
        // annual taxable = $72,000 − $25,500 = $46,500
        // annual tax = $46,500 × 0.045 = $2,092.50
        // per period = $2,092.50 / 12 = $174.375 → $174.38
        var result = Calculate(GrossWages: 6_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(6_000m, result.TaxableWages);
        Assert.Equal(174.38m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,500 × 12 = $18,000; std ded = $25,500
        // annual taxable = max(0, $18,000 − $25,500) = $0
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household ────────────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_BasicWithholding()
    {
        // annual = $4,000 × 12 = $48,000
        // std ded = $19,125
        // annual taxable = $48,000 − $19,125 = $28,875
        // annual tax = $28,875 × 0.045 = $1,299.375
        // per period = $1,299.375 / 12 = $108.28125 → $108.28
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(108.28m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_BasicWithholding()
    {
        // annual = $2,000 × 26 = $52,000
        // std ded = $19,125
        // annual taxable = $52,000 − $19,125 = $32,875
        // annual tax = $32,875 × 0.045 = $1,479.375
        // per period = $1,479.375 / 26 = $56.898076... → $56.90
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(56.90m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,200 × 12 = $14,400; HoH std ded = $19,125
        // annual taxable = max(0, $14,400 − $19,125) = $0
        var result = Calculate(GrossWages: 1_200m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── NC-4 Allowances ──────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_ReducesTax()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $12,750; 1 allowance = $2,500
        // annual taxable = $26,000 − $12,750 − $2,500 = $10,750
        // annual tax = $10,750 × 0.045 = $483.75
        // per period = $483.75 / 26 = $18.605769... → $18.61
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(18.61m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReduceTaxFurther()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $12,750; 2 allowances = $5,000
        // annual taxable = $26,000 − $12,750 − $5,000 = $8,250
        // annual tax = $8,250 × 0.045 = $371.25
        // per period = $371.25 / 26 = $14.278846... → $14.28
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(14.28m, result.Withholding);
    }

    [Fact]
    public void Allowances_EliminateAllTax_ReturnsZero()
    {
        // annual = $1,000 × 26 = $26,000; std ded = $12,750
        // 6 allowances = $15,000 → taxable = max(0, $26,000 − $12,750 − $15,000) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 6);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TwoAllowances()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = $25,500; 2 allowances = $5,000
        // annual taxable = $78,000 − $25,500 − $5,000 = $47,500
        // annual tax = $47,500 × 0.045 = $2,137.50
        // per period = $2,137.50 / 26 = $82.211538... → $82.21
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Married", allowances: 2);

        Assert.Equal(82.21m, result.Withholding);
    }

    // ── Standard deduction boundary ──────────────────────────────────

    [Fact]
    public void Single_Annual_ExactlyAtStandardDeduction_ReturnsZero()
    {
        // annual = $12,750; std ded = $12,750
        // annual taxable = max(0, $12,750 − $12,750) = $0
        var result = Calculate(GrossWages: 12_750m, PayFrequency.Annual, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_OneAboveStandardDeduction()
    {
        // annual = $15,000; std ded = $12,750
        // annual taxable = $15,000 − $12,750 = $2,250
        // annual tax = $2,250 × 0.045 = $101.25
        // per period = $101.25 / 1 = $101.25
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Annual, "Single");

        Assert.Equal(101.25m, result.Withholding);
    }

    [Fact]
    public void Married_Annual_ExactlyAtStandardDeduction_ReturnsZero()
    {
        // annual = $25,500; std ded = $25,500
        // annual taxable = max(0, $25,500 − $25,500) = $0
        var result = Calculate(GrossWages: 25_500m, PayFrequency.Annual, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Annual_ExactlyAtStandardDeduction_ReturnsZero()
    {
        // annual = $19,125; HoH std ded = $19,125
        // annual taxable = max(0, $19,125 − $19,125) = $0
        var result = Calculate(GrossWages: 19_125m, PayFrequency.Annual, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_BasicWithholding = $177.19; extra = $20.00 → $197.19
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 20m);

        Assert.Equal(197.19m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual = $800 × 26 = $20,800
        // std ded = $12,750
        // annual taxable = $20,800 − $12,750 = $8,050
        // annual tax = $8,050 × 0.045 = $362.25
        // per period = $362.25 / 26 = $13.932692... → $13.93
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(13.93m, result.Withholding);
    }

    // ── Semimonthly pay frequency ────────────────────────────────────

    [Fact]
    public void Single_Semimonthly_CorrectDeannualization()
    {
        // annual = $2,000 × 24 = $48,000
        // std ded = $12,750
        // annual taxable = $48,000 − $12,750 = $35,250
        // annual tax = $35,250 × 0.045 = $1,586.25
        // per period = $1,586.25 / 24 = $66.09375 → $66.09
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Semimonthly, "Single");

        Assert.Equal(66.09m, result.Withholding);
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
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    // ── Explanation ──────────────────────────────────────────────────

    [Fact]
    public void Explanation_Single_ShowsDeductionFlatRateAndDeannualization()
    {
        // $2,000 biweekly Single, 0 allowances:
        // annualize = 52,000; std ded = 12,750; annual taxable = 39,250
        // annual tax = 39,250 × 4.5% = 1,766.25; per period = 67.932... → 67.93
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single");

        Assert.NotNull(result.WithholdingSteps);
        var annualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annualize wages"));
        Assert.Equal(52_000m, annualizeStep.Value);
        var dedStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Less standard deduction"));
        Assert.Equal(12_750m, dedStep.Value);
        var taxableStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual taxable income");
        Assert.Equal(39_250m, taxableStep.Value);
        var rateStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("4.50%"));
        Assert.Equal(1_766.25m, rateStep.Value);
        var deannualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "De-annualize to this pay period");
        Assert.Equal(67.93m, deannualizeStep.Value);
        Assert.Contains("NC-30", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_Allowances_EmitAllowanceDeductionStep()
    {
        // 2 allowances × $2,500 = $5,000 annual deduction
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        var allowanceStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Less NC-4 allowance deduction");
        Assert.Equal(5_000m, allowanceStep.Value);
    }

    [Fact]
    public void Explanation_NoAllowances_OmitsAllowanceDeductionStep()
    {
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single");

        Assert.DoesNotContain(result.WithholdingSteps!, s => s.Label == "Less NC-4 allowance deduction");
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
        var calc = new NorthCarolinaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.NC,
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
