using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.RhodeIsland;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Rhode Island (RI) state income tax withholding.
/// Rhode Island uses the dedicated <see cref="RhodeIslandWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the RI annualized
/// percentage-method formula (2026) per RI Division of Taxation Pub. T-174:
///   taxable wages   = gross wages − pre-tax deductions (floor $0)
///   annual wages    = taxable wages × pay periods
///   annual taxable  = max(0, annual wages − $10,550 − (exemptions × $4,700))
///   annual tax      = ApplyBrackets(annual taxable)
///   per-period      = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Rhode Island parameters:
///   Standard deduction (all filing statuses): $10,550
///   Per-exemption deduction (RI W-4 Line 2):  $4,700
///   Brackets (all filing statuses):
///     3.75% on $0 – $77,450
///     4.75% on $77,450 – $176,050
///     5.99% over $176,050
/// </summary>
public class RhodeIslandWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsRhodeIsland()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.RI, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Exemptions_AdditionalWithholding()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeAllThree()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
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
    public void Single_Biweekly_BasicWithholding()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $10,550; 0 exemptions = $0
        // annual taxable = $26,000 − $10,550 = $15,450
        // annual tax = $15,450 × 3.75% = $579.375
        // per period = $579.375 / 26 = $22.283653... → $22.28
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(22.28m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BasicWithholding()
    {
        // annual = $5,000 × 12 = $60,000
        // std ded = $10,550
        // annual taxable = $60,000 − $10,550 = $49,450
        // annual tax: first $49,450 (all in bracket 1) × 3.75% = $1,854.375
        // per period = $1,854.375 / 12 = $154.53125 → $154.53
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(154.53m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $750 × 12 = $9,000; std ded = $10,550
        // annual taxable = max(0, $9,000 − $10,550) = $0
        var result = Calculate(GrossWages: 750m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married ─────────────────────────────────────────────────────

    [Fact]
    public void Married_Monthly_BasicWithholding()
    {
        // Rhode Island uses the same std ded and brackets for Married as for Single.
        // annual = $5,000 × 12 = $60,000
        // std ded = $10,550
        // annual taxable = $60,000 − $10,550 = $49,450
        // annual tax = $49,450 × 3.75% = $1,854.375
        // per period = $1,854.375 / 12 = $154.53125 → $154.53
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(154.53m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $700 × 12 = $8,400; std ded = $10,550
        // annual taxable = max(0, $8,400 − $10,550) = $0
        var result = Calculate(GrossWages: 700m, PayFrequency.Monthly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household ────────────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_BasicWithholding()
    {
        // Rhode Island uses the same std ded and brackets for HoH.
        // annual = $5,000 × 12 = $60,000
        // std ded = $10,550
        // annual taxable = $49,450
        // annual tax = $49,450 × 3.75% = $1,854.375
        // per period = $1,854.375 / 12 = $154.53125 → $154.53
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(154.53m, result.Withholding);
    }

    // ── Second and third brackets ────────────────────────────────────

    [Fact]
    public void Single_Monthly_SecondBracket()
    {
        // annual = $8,500 × 12 = $102,000
        // std ded = $10,550
        // annual taxable = $102,000 − $10,550 = $91,450
        // bracket 1: $77,450 × 3.75% = $2,904.375
        // bracket 2: ($91,450 − $77,450) × 4.75% = $14,000 × 4.75% = $665.00
        // annual tax = $2,904.375 + $665.00 = $3,569.375
        // per period = $3,569.375 / 12 = $297.447916... → $297.45
        var result = Calculate(GrossWages: 8_500m, PayFrequency.Monthly, "Single");

        Assert.Equal(297.45m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_ThirdBracket()
    {
        // annual = $18,000 × 12 = $216,000
        // std ded = $10,550
        // annual taxable = $216,000 − $10,550 = $205,450
        // bracket 1: $77,450 × 3.75% = $2,904.375
        // bracket 2: ($176,050 − $77,450) × 4.75% = $98,600 × 4.75% = $4,683.50
        // bracket 3: ($205,450 − $176,050) × 5.99% = $29,400 × 5.99% = $1,761.06
        // annual tax = $2,904.375 + $4,683.50 + $1,761.06 = $9,348.935
        // per period = $9,348.935 / 12 = $779.07791... → $779.08
        var result = Calculate(GrossWages: 18_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(779.08m, result.Withholding);
    }

    // ── Bracket boundary tests ───────────────────────────────────────

    [Fact]
    public void Single_Annual_ExactlyAtFirstBracketCeiling()
    {
        // annual = $77,450 + std ded $10,550 = $88,000
        // annual taxable = $77,450 (exactly at ceiling)
        // annual tax = $77,450 × 3.75% = $2,904.375
        // per period (annual) = $2,904.375 / 1 = $2,904.375 → $2,904.38
        var result = Calculate(GrossWages: 88_000m, PayFrequency.Annual, "Single");

        Assert.Equal(2_904.38m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_ExactlyAtSecondBracketCeiling()
    {
        // annual = $176,050 + std ded $10,550 = $186,600
        // annual taxable = $176,050
        // bracket 1: $77,450 × 3.75% = $2,904.375
        // bracket 2: ($176,050 − $77,450) × 4.75% = $98,600 × 4.75% = $4,683.50
        // annual tax = $2,904.375 + $4,683.50 = $7,587.875
        // per period = $7,587.875 → $7,587.88
        var result = Calculate(GrossWages: 186_600m, PayFrequency.Annual, "Single");

        Assert.Equal(7_587.88m, result.Withholding);
    }

    // ── RI W-4 Exemptions ────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneExemption_ReducesTax()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $10,550; 1 exemption = $4,700
        // annual taxable = $26,000 − $10,550 − $4,700 = $10,750
        // annual tax = $10,750 × 3.75% = $403.125
        // per period = $403.125 / 26 = $15.504807... → $15.50
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 1);

        Assert.Equal(15.50m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoExemptions_ReduceTaxFurther()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $10,550; 2 exemptions = $9,400
        // annual taxable = $26,000 − $10,550 − $9,400 = $6,050
        // annual tax = $6,050 × 3.75% = $226.875
        // per period = $226.875 / 26 = $8.726923... → $8.73
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 2);

        Assert.Equal(8.73m, result.Withholding);
    }

    [Fact]
    public void Exemptions_EliminateAllTax_ReturnsZero()
    {
        // annual = $1,000 × 26 = $26,000; std ded = $10,550
        // 4 exemptions = $18,800 → taxable = max(0, $26,000 − $10,550 − $18,800) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 4);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TwoExemptions()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = $10,550; 2 exemptions = $9,400
        // annual taxable = $78,000 − $10,550 − $9,400 = $58,050
        // annual tax = $58,050 × 3.75% = $2,176.875
        // per period = $2,176.875 / 26 = $83.726923... → $83.73
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Married", exemptions: 2);

        Assert.Equal(83.73m, result.Withholding);
    }

    // ── Standard deduction boundary ──────────────────────────────────

    [Fact]
    public void Single_Annual_ExactlyAtStandardDeduction_ReturnsZero()
    {
        // annual = $10,550; std ded = $10,550
        // annual taxable = max(0, $10,550 − $10,550) = $0
        var result = Calculate(GrossWages: 10_550m, PayFrequency.Annual, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_Annual_OneAboveStandardDeduction()
    {
        // annual = $15,000; std ded = $10,550
        // annual taxable = $4,450
        // annual tax = $4,450 × 3.75% = $166.875
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Annual, "Single");

        Assert.Equal(166.88m, result.Withholding);
    }

    // ── Filing statuses produce identical results (RI-specific) ──────

    [Fact]
    public void AllFilingStatuses_ProduceSameResult_ForSameInputs()
    {
        // RI uses the same std ded and brackets for all statuses.
        var single = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Single");
        var married = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Married");
        var hoh = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(single.Withholding, married.Withholding);
        Assert.Equal(single.Withholding, hoh.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_BasicWithholding = $154.53; extra = $20.00 → $174.53
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 20m);

        Assert.Equal(174.53m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual = $800 × 26 = $20,800
        // std ded = $10,550
        // annual taxable = $20,800 − $10,550 = $10,250
        // annual tax = $10,250 × 3.75% = $384.375
        // per period = $384.375 / 26 = $14.783653... → $14.78
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(14.78m, result.Withholding);
    }

    // ── Semimonthly pay frequency ────────────────────────────────────

    [Fact]
    public void Single_Semimonthly_CorrectDeannualization()
    {
        // annual = $2,000 × 24 = $48,000
        // std ded = $10,550
        // annual taxable = $48,000 − $10,550 = $37,450
        // annual tax = $37,450 × 3.75% = $1,404.375
        // per period = $1,404.375 / 24 = $58.515625 → $58.52
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Semimonthly, "Single");

        Assert.Equal(58.52m, result.Withholding);
    }

    // ── Zero gross wages ─────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── RI not in generic StateTaxConfigs2026 ────────────────────────

    [Fact]
    public void RhodeIsland_UsesDedicatedCalculator_NotInGenericConfigs()
    {
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.RI),
            "RI should not be in generic StateTaxConfigs2026 — it uses RhodeIslandWithholdingCalculator.");
    }

    // ── Validation ───────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = -1
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Exemptions", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Exemptions"] = 2,
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
        int exemptions = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new RhodeIslandWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.RI,
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
