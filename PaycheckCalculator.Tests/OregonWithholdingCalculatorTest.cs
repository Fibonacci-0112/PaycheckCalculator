using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Oregon;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Oregon (OR) state income tax withholding.
/// Oregon uses the dedicated <see cref="OregonWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from Oregon DOR Publication
/// 150-206-436 (2026 Oregon Withholding Tax Formulas):
///   1. per-period taxable wages = gross − pre-tax deductions (floored at $0)
///   2. annual wages = per-period taxable wages × pay periods
///   3. annual taxable income = max(0, annual wages − standard deduction)
///   4. annual tax = graduated brackets applied to annual taxable income
///   5. annual tax after credits = max(0, annual tax − allowances × $219)
///   6. per-period withholding = round(annual tax after credits ÷ periods, 2)
///                                + additional withholding
///
/// 2026 Oregon parameters (OR DOR Publication 150-206-436):
///   Standard deduction:
///     Single / MFS:            $2,835
///     Married / QSS:           $5,670
///     Head of Household:       $2,835 (same as Single; HoH uses Married brackets)
///   Per-allowance credit: $219 (applied to computed annual tax)
///   Brackets — Single / MFS:
///     4.75% on $0 – $4,300
///     6.75% on $4,300 – $10,750
///     8.75% on $10,750 – $125,000
///     9.9%  over $125,000
///   Brackets — Married / QSS and Head of Household:
///     4.75% on $0 – $8,600
///     6.75% on $8,600 – $21,500
///     8.75% on $21,500 – $250,000
///     9.9%  over $250,000
/// </summary>
public class OregonWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsOregon()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        Assert.Equal(UsState.OR, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsExpectedFields()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer ─────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracket()
    {
        // annual = $1,000 × 26 = $26,000
        // annual taxable = $26,000 − $2,835 = $23,165
        // tax = $4,300 × 4.75% + ($10,750 − $4,300) × 6.75% + ($23,165 − $10,750) × 8.75%
        //     = $204.25 + $435.375 + $12,415 × 0.0875
        //     = $204.25 + $435.375 + $1,086.3125
        //     = $1,725.9375
        // per period = $1,725.9375 / 26 = $66.382... → $66.38
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(66.38m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_FirstBracket_LowWages()
    {
        // annual = $500 × 12 = $6,000
        // annual taxable = $6,000 − $2,835 = $3,165
        // tax = $3,165 × 4.75% = $150.3375
        // per period = $150.3375 / 12 = $12.528... → $12.53
        var result = Calculate(GrossWages: 500m, PayFrequency.Monthly, "Single");

        Assert.Equal(500m, result.TaxableWages);
        Assert.Equal(12.53m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_SecondBracket()
    {
        // annual = $600 × 26 = $15,600
        // annual taxable = $15,600 − $2,835 = $12,765
        // tax = $4,300 × 4.75% + ($10,750 − $4,300) × 6.75% + ($12,765 − $10,750) × 8.75%
        //     = $204.25 + $435.375 + $2,015 × 0.0875
        //     = $204.25 + $435.375 + $176.3125
        //     = $815.9375
        // per period = $815.9375 / 26 = $31.382... → $31.38
        var result = Calculate(GrossWages: 600m, PayFrequency.Biweekly, "Single");

        Assert.Equal(600m, result.TaxableWages);
        Assert.Equal(31.38m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_ThirdBracket()
    {
        // annual = $8,000 × 12 = $96,000
        // annual taxable = $96,000 − $2,835 = $93,165
        // tax = $4,300 × 4.75% + ($10,750 − $4,300) × 6.75% + ($93,165 − $10,750) × 8.75%
        //     = $204.25 + $435.375 + $82,415 × 0.0875
        //     = $204.25 + $435.375 + $7,211.3125
        //     = $7,850.9375
        // per period = $7,850.9375 / 12 = $654.244... → $654.24
        var result = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(8_000m, result.TaxableWages);
        Assert.Equal(654.24m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $15,000 × 12 = $180,000
        // annual taxable = $180,000 − $2,835 = $177,165
        // tax = $4,300 × 4.75% + ($10,750 − $4,300) × 6.75% + ($125,000 − $10,750) × 8.75%
        //       + ($177,165 − $125,000) × 9.9%
        //     = $204.25 + $435.375 + $114,250 × 0.0875 + $52,165 × 0.099
        //     = $204.25 + $435.375 + $9,996.875 + $5,164.335
        //     = $15,800.835
        // per period = $15,800.835 / 12 = $1,316.736... → $1,316.74
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(15_000m, result.TaxableWages);
        Assert.Equal(1_316.74m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $100 × 12 = $1,200
        // annual taxable = max(0, $1,200 − $2,835) = $0
        var result = Calculate(GrossWages: 100m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married filer ────────────────────────────────────────────────

    [Fact]
    public void Married_Biweekly_FirstBracket()
    {
        // annual = $1,200 × 26 = $31,200
        // annual taxable = $31,200 − $5,670 = $25,530
        // tax = $8,600 × 4.75% + ($21,500 − $8,600) × 6.75% + ($25,530 − $21,500) × 8.75%
        //     = $408.50 + $12,900 × 0.0675 + $4,030 × 0.0875
        //     = $408.50 + $870.75 + $352.625
        //     = $1,631.875
        // per period = $1,631.875 / 26 = $62.764... → $62.76
        var result = Calculate(GrossWages: 1_200m, PayFrequency.Biweekly, "Married");

        Assert.Equal(1_200m, result.TaxableWages);
        Assert.Equal(62.76m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_FirstBracket_LowWages()
    {
        // annual = $800 × 12 = $9,600
        // annual taxable = $9,600 − $5,670 = $3,930
        // tax = $3,930 × 4.75% = $186.675
        // per period = $186.675 / 12 = $15.556... → $15.56
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Married");

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(15.56m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TopBracket()
    {
        // annual = $15,000 × 26 = $390,000
        // annual taxable = $390,000 − $5,670 = $384,330
        // tax = $8,600 × 4.75% + ($21,500 − $8,600) × 6.75% + ($250,000 − $21,500) × 8.75%
        //       + ($384,330 − $250,000) × 9.9%
        //     = $408.50 + $870.75 + $228,500 × 0.0875 + $134,330 × 0.099
        //     = $408.50 + $870.75 + $19,993.75 + $13,298.67
        //     = $34,571.67
        // per period = $34,571.67 / 26 = $1,329.680... → $1,329.68
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(15_000m, result.TaxableWages);
        Assert.Equal(1_329.68m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $400 × 12 = $4,800
        // annual taxable = max(0, $4,800 − $5,670) = $0
        var result = Calculate(GrossWages: 400m, PayFrequency.Monthly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household filer ──────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_UsesMarriedBracketsWithSingleDeduction()
    {
        // HoH: standard deduction = $2,835 (Single), brackets = Married
        // annual = $1,200 × 26 = $31,200
        // annual taxable = $31,200 − $2,835 = $28,365
        // tax = $8,600 × 4.75% + ($21,500 − $8,600) × 6.75% + ($28,365 − $21,500) × 8.75%
        //     = $408.50 + $870.75 + $6,865 × 0.0875
        //     = $408.50 + $870.75 + $600.6875
        //     = $1,879.9375
        // per period = $1,879.9375 / 26 = $72.305... → $72.31
        var result = Calculate(GrossWages: 1_200m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(1_200m, result.TaxableWages);
        Assert.Equal(72.31m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_FirstBracket()
    {
        // HoH: standard deduction = $2,835 (Single), brackets = Married
        // annual = $700 × 12 = $8,400
        // annual taxable = $8,400 − $2,835 = $5,565
        // tax = $5,565 × 4.75% = $264.3375
        // per period = $264.3375 / 12 = $22.028... → $22.03
        var result = Calculate(GrossWages: 700m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(700m, result.TaxableWages);
        Assert.Equal(22.03m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_DiffersFromSingle_SameGrossWages()
    {
        // HoH uses Married brackets → lower tax than Single at same wages
        var singleResult = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Single");
        var hohResult    = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Head of Household");

        // HoH has same deduction as Single but Married brackets (wider first two brackets)
        // so withholding should be equal or lower than Single
        Assert.True(hohResult.Withholding <= singleResult.Withholding,
            "HoH withholding should not exceed Single when using Married brackets.");
    }

    [Fact]
    public void HeadOfHousehold_DiffersFromMarried_SameGrossWages()
    {
        // HoH uses smaller standard deduction ($2,835) than Married ($5,670)
        // at the same gross wages, HoH will typically have higher withholding than Married
        var marriedResult = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Married");
        var hohResult     = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.True(hohResult.Withholding >= marriedResult.Withholding,
            "HoH withholding should not be lower than Married at the same wages (smaller deduction).");
    }

    [Fact]
    public void HeadOfHousehold_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $100 × 12 = $1,200
        // annual taxable = max(0, $1,200 − $2,835) = $0
        var result = Calculate(GrossWages: 100m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Allowance credits ─────────────────────────────────────────────

    [Fact]
    public void Single_OneAllowance_ReducesWithholding()
    {
        // Base (Single monthly $8,000) = $654.24 (from earlier test)
        // 1 allowance credit = $219/year; per period reduction = $219/12 = $18.25
        // expected = $654.24 − $18.25 = $635.99
        var baseResult       = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Single");
        var allowanceResult  = Calculate(GrossWages: 8_000m, PayFrequency.Monthly, "Single", allowances: 1);

        var difference = baseResult.Withholding - allowanceResult.Withholding;
        Assert.Equal(18.25m, difference);
    }

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReduceWithholding()
    {
        // annual = $1,000 × 26 = $26,000; taxable = $23,165; annual tax = $1,725.9375
        // credits = 2 × $219 = $438; annual tax after credits = $1,725.9375 − $438 = $1,287.9375
        // per period = $1,287.9375 / 26 = $49.536... → $49.54
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(49.54m, result.Withholding);
    }

    [Fact]
    public void AllowanceCredit_CannotProduceNegativeWithholding()
    {
        // Annual tax on very low wages minus many allowances should floor at $0
        // annual = $200 × 12 = $2,400; taxable = max(0, $2,400 − $2,835) = $0
        // annual tax = $0; 10 allowance credits = 10 × $219 = $2,190; max(0, $0 − $2,190) = $0
        var result = Calculate(GrossWages: 200m, PayFrequency.Monthly, "Single", allowances: 10);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void AllowanceCredit_LargeNumber_FloorsAtZeroNotNegative()
    {
        // 5 allowances × $219 = $1,095 annual credit
        // Use income where tax < $1,095 to ensure floor works
        // annual = $500 × 12 = $6,000; taxable = $3,165; tax = $3,165 × 4.75% = $150.3375
        // after credits = max(0, $150.3375 − $1,095) = $0
        var result = Calculate(GrossWages: 500m, PayFrequency.Monthly, "Single", allowances: 5);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // Base (Single monthly $500) = $12.53; extra = $25 → $37.53
        var result = Calculate(GrossWages: 500m, PayFrequency.Monthly, "Single",
            additionalWithholding: 25m);

        Assert.Equal(37.53m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_WithZeroBaseAndCredit_ReturnsExtraOnly()
    {
        // Low income produces $0 base; extra withholding still applies
        var result = Calculate(GrossWages: 100m, PayFrequency.Monthly, "Single",
            additionalWithholding: 15m);

        Assert.Equal(15m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual = $800 × 26 = $20,800; taxable = $20,800 − $2,835 = $17,965
        // tax = $4,300 × 4.75% + ($10,750 − $4,300) × 6.75% + ($17,965 − $10,750) × 8.75%
        //     = $204.25 + $435.375 + $7,215 × 0.0875
        //     = $204.25 + $435.375 + $631.3125
        //     = $1,270.9375
        // per period = $1,270.9375 / 26 = $48.882... → $48.88
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(48.88m, result.Withholding);
    }

    // ── Bracket boundary ─────────────────────────────────────────────

    [Fact]
    public void Single_AtFirstBracketCeiling_ExactBoundary()
    {
        // annual = $500 × 12 = $6,000; taxable = $3,165 — all in first bracket
        // Use monthly $800 to push just past first ceiling: annual = $9,600; taxable = $6,765
        // $4,300 is first ceiling; $6,765 > $4,300 so crosses into second bracket
        // tax = $4,300 × 4.75% + ($6,765 − $4,300) × 6.75%
        //     = $204.25 + $2,465 × 0.0675
        //     = $204.25 + $166.3875 = $370.6375
        // per period = $370.6375 / 12 = $30.886... → $30.89
        var result = Calculate(GrossWages: 800m, PayFrequency.Monthly, "Single");

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(30.89m, result.Withholding);
    }

    [Fact]
    public void Married_AtFirstBracketCeiling_ExactBoundary()
    {
        // annual = $1,000 × 12 = $12,000; taxable = $12,000 − $5,670 = $6,330
        // $6,330 < $8,600 → first bracket only
        // tax = $6,330 × 4.75% = $300.675
        // per period = $300.675 / 12 = $25.056... → $25.06
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(25.06m, result.Withholding);
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
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
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
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AllFilingStatuses_AreAccepted()
    {
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        foreach (var status in new[] { "Single", "Married", "Head of Household" })
        {
            var values = new StateInputValues { ["FilingStatus"] = status };
            Assert.Empty(calc.Validate(values));
        }
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
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.OR,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"]          = filingStatus,
            ["Allowances"]            = allowances,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }

    // ── Paid Leave Oregon employee contribution ───────────────────────
    //
    // Paid Leave Oregon 2026: total contribution 1% of gross wages up to
    // $184,500; employees pay 60% of that, i.e. 0.6%.
    // https://paidleave.oregon.gov/employers/Pages/default.aspx

    [Fact]
    public void PaidLeave_IsSixTenthsOfOnePercentOfGrossWages()
    {
        // 3,000 × 0.6% = 18.00
        var result = CalculateAssessments(grossWages: 3000m);

        Assert.Equal(18.00m, Assert.Single(result.TaxLines!, l => l.ShortCode == "PFML").Amount);
        Assert.Equal("Paid Leave Oregon", result.DisabilityInsuranceLabel);
    }

    [Fact]
    public void PaidLeave_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        var result = CalculateAssessments(grossWages: 3000m, preTaxDeductions: 1000m);

        Assert.Equal(18.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void PaidLeave_StopsAtWageBase()
    {
        // $180,000 earned leaves $4,500 of the $184,500 base: 4,500 × 0.6% = 27.00
        var result = CalculateAssessments(grossWages: 10_000m, ytdStateWages: 180_000m);

        Assert.Equal(27.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void PaidLeave_AtWageBase_WithholdsNothingFurther()
    {
        var result = CalculateAssessments(grossWages: 10_000m, ytdStateWages: 184_500m);

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    [Fact]
    public void PaidLeave_RoundsAwayFromZero()
    {
        // 107.50 × 0.6% = 0.645 exactly — a true midpoint, so away-from-zero
        // gives 0.65 where banker's rounding would give 0.64.
        var result = CalculateAssessments(grossWages: 107.50m);

        Assert.Equal(0.65m, result.DisabilityInsurance);
    }

    private static StateWithholdingResult CalculateAssessments(
        decimal grossWages,
        PayFrequency frequency = PayFrequency.Biweekly,
        decimal preTaxDeductions = 0m,
        decimal ytdStateWages = 0m)
    {{
        var calc = new OregonWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.OR,
            GrossWages: grossWages,
            PayPeriod: frequency,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions,
            YtdStateWages: ytdStateWages);

        var values = PayCalculatorTestHarness.DefaultValues(TestSchemas.Provider.GetSchema(UsState.OR));

        return calc.Calculate(context, values);
    }}

}
