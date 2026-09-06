using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Maryland;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Maryland (MD) state income tax withholding.
/// Maryland uses the dedicated <see cref="MarylandWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Comptroller of Maryland
/// annualized percentage-method formula (2026 Employer Withholding Guide):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standardDeduction(annualWages) − (exemptions × $3,200))
///   standardDeduction = max(min(annualWages × 15%, stdMax), stdMin)
///     Single:  stdMin = $1,600,  stdMax = $2,550
///     Married / Head of Household:  stdMin = $3,200,  stdMax = $5,100
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Maryland parameters:
///   Per-exemption deduction: $3,200
///   Single rate schedule:
///     2.00% on $0–$1,000 | 3.00% on $1,001–$2,000 | 4.00% on $2,001–$3,000
///     4.75% on $3,001–$100,000 | 5.00% on $100,001–$125,000
///     5.25% on $125,001–$150,000 | 5.50% on $150,001–$250,000
///     5.75% on $250,001–$500,000 | 6.25% on $500,001–$1,000,000
///     6.50% over $1,000,000
///   Married / Head of Household rate schedule:
///     2.00% on $0–$1,000 | 3.00% on $1,001–$2,000 | 4.00% on $2,001–$3,000
///     4.75% on $3,001–$150,000 | 5.00% on $150,001–$175,000
///     5.25% on $175,001–$225,000 | 5.50% on $225,001–$300,000
///     5.75% on $300,001–$600,000 | 6.25% on $600,001–$1,200,000
///     6.50% over $1,200,000
/// </summary>
public class MarylandWithholdingCalculatorTest
{
    // ── State identity ───────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMaryland()
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        Assert.Equal(UsState.MD, calc.State);
    }

    // ── Schema ───────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_County_Exemptions_AdditionalWithholding()
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var schema = calc.GetInputSchema();

        Assert.Equal(4, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "County");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsSingleMarriedHoH()
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer — standard deduction at maximum ($2,550) ────────

    [Fact]
    public void Single_Biweekly_StdDedAtMax_FourthBracketOnly()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = max(min($78,000 × 15%, $2,550), $1,600) = max($2,550, $1,600) = $2,550
        // taxable = $78,000 − $2,550 = $75,450
        // $0–$1,000 @ 2%      = $20.00
        // $1,000–$2,000 @ 3%  = $30.00
        // $2,000–$3,000 @ 4%  = $40.00
        // $3,000–$75,450 @ 4.75% = $72,450 × 0.0475 = $3,441.375
        // total = $3,531.375
        // per period = $3,531.375 / 26 = $135.8221… → $135.82
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(135.82m, StateIncome(result));
    }

    // ── Single filer — standard deduction in variable range ──────────

    [Fact]
    public void Single_Biweekly_StdDedVariable_BetweenMinAndMax()
    {
        // annual = $500 × 26 = $13,000
        // std ded = max(min($13,000 × 15%, $2,550), $1,600)
        //         = max(min($1,950, $2,550), $1,600) = max($1,950, $1,600) = $1,950
        // taxable = $13,000 − $1,950 = $11,050
        // $0–$1,000 @ 2%     = $20.00
        // $1,000–$2,000 @ 3% = $30.00
        // $2,000–$3,000 @ 4% = $40.00
        // $3,000–$11,050 @ 4.75% = $8,050 × 0.0475 = $382.375
        // total = $472.375
        // per period = $472.375 / 26 = $18.1682… → $18.17
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly, "Single");

        Assert.Equal(18.17m, StateIncome(result));
    }

    // ── Single filer — standard deduction at minimum ($1,600) ────────

    [Fact]
    public void Single_Biweekly_StdDedAtMin()
    {
        // annual = $200 × 26 = $5,200
        // std ded = max(min($5,200 × 15%, $2,550), $1,600)
        //         = max(min($780, $2,550), $1,600) = max($780, $1,600) = $1,600
        // taxable = $5,200 − $1,600 = $3,600
        // $0–$1,000 @ 2%     = $20.00
        // $1,000–$2,000 @ 3% = $30.00
        // $2,000–$3,000 @ 4% = $40.00
        // $3,000–$3,600 @ 4.75% = $600 × 0.0475 = $28.50
        // total = $118.50
        // per period = $118.50 / 26 = $4.5577… → $4.56
        var result = Calculate(GrossWages: 200m, PayFrequency.Biweekly, "Single");

        Assert.Equal(4.56m, StateIncome(result));
    }

    // ── Single filer — upper bracket (5th bracket) ───────────────────

    [Fact]
    public void Single_Monthly_HighIncome_AllBrackets()
    {
        // annual = $25,000 × 12 = $300,000
        // std ded = max(min($300,000 × 15%, $2,550), $1,600) = $2,550
        // taxable = $297,450
        // $0–$1,000 @ 2%         = $20.00
        // $1,000–$2,000 @ 3%     = $30.00
        // $2,000–$3,000 @ 4%     = $40.00
        // $3,000–$100,000 @ 4.75%     = $97,000 × 0.0475 = $4,607.50
        // $100,000–$125,000 @ 5.00%   = $25,000 × 0.05   = $1,250.00
        // $125,000–$150,000 @ 5.25%   = $25,000 × 0.0525 = $1,312.50
        // $150,000–$250,000 @ 5.50%   = $100,000 × 0.055 = $5,500.00
        // $250,000–$297,450 @ 5.75%   = $47,450 × 0.0575 = $2,728.375
        // total = $15,488.375
        // per period = $15,488.375 / 12 = $1,290.6979… → $1,290.70
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_290.70m, StateIncome(result));
    }

    // ── Married filer — standard deduction at maximum ($5,100) ───────

    [Fact]
    public void Married_Biweekly_StdDedAtMax_FourthBracketOnly()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = max(min($78,000 × 15%, $5,100), $3,200) = max($5,100, $3,200) = $5,100
        // taxable = $78,000 − $5,100 = $72,900
        // $0–$1,000 @ 2%     = $20.00
        // $1,000–$2,000 @ 3% = $30.00
        // $2,000–$3,000 @ 4% = $40.00
        // $3,000–$72,900 @ 4.75% = $69,900 × 0.0475 = $3,320.25
        // total = $3,410.25
        // per period = $3,410.25 / 26 = $131.1634… → $131.16
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(131.16m, StateIncome(result));
    }

    // ── Married filer — standard deduction at minimum ($3,200) ───────

    [Fact]
    public void Married_Monthly_StdDedAtMin()
    {
        // annual = $600 × 12 = $7,200
        // std ded = max(min($7,200 × 15%, $5,100), $3,200)
        //         = max(min($1,080, $5,100), $3,200) = max($1,080, $3,200) = $3,200
        // taxable = $7,200 − $3,200 = $4,000
        // $0–$1,000 @ 2%     = $20.00
        // $1,000–$2,000 @ 3% = $30.00
        // $2,000–$3,000 @ 4% = $40.00
        // $3,000–$4,000 @ 4.75% = $1,000 × 0.0475 = $47.50
        // total = $137.50
        // per period = $137.50 / 12 = $11.4583… → $11.46
        var result = Calculate(GrossWages: 600m, PayFrequency.Monthly, "Married");

        Assert.Equal(11.46m, StateIncome(result));
    }

    // ── Married filer — high income, all brackets ─────────────────────

    [Fact]
    public void Married_Monthly_HighIncome_AllBrackets()
    {
        // annual = $25,000 × 12 = $300,000
        // std ded = max(min($300,000 × 15%, $5,100), $3,200) = $5,100
        // taxable = $294,900
        // $0–$1,000 @ 2%          = $20.00
        // $1,000–$2,000 @ 3%      = $30.00
        // $2,000–$3,000 @ 4%      = $40.00
        // $3,000–$150,000 @ 4.75%      = $147,000 × 0.0475 = $6,982.50
        // $150,000–$175,000 @ 5.00%    = $25,000 × 0.05    = $1,250.00
        // $175,000–$225,000 @ 5.25%    = $50,000 × 0.0525  = $2,625.00
        // $225,000–$294,900 @ 5.50%    = $69,900 × 0.055   = $3,844.50
        // total = $14,792.00
        // per period = $14,792.00 / 12 = $1,232.6667… → $1,232.67
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(1_232.67m, StateIncome(result));
    }

    // ── Head of Household — uses married limits and brackets ──────────

    [Fact]
    public void HeadOfHousehold_Monthly_UsesMheMarriedSchedule()
    {
        // annual = $5,000 × 12 = $60,000
        // std ded (married/HoH) = max(min($60,000 × 15%, $5,100), $3,200)
        //                       = max(min($9,000, $5,100), $3,200) = max($5,100, $3,200) = $5,100
        // taxable = $60,000 − $5,100 = $54,900
        // $0–$1,000 @ 2%        = $20.00
        // $1,000–$2,000 @ 3%    = $30.00
        // $2,000–$3,000 @ 4%    = $40.00
        // $3,000–$54,900 @ 4.75% = $51,900 × 0.0475 = $2,465.25
        // total = $2,555.25
        // per period = $2,555.25 / 12 = $212.9375 → $212.94
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(212.94m, StateIncome(result));
    }

    // ── Exemptions ───────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_TwoExemptions_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = $2,550
        // exemptions = 2 × $3,200 = $6,400
        // taxable = $78,000 − $2,550 − $6,400 = $69,050
        // $0–$1,000 @ 2%         = $20.00
        // $1,000–$2,000 @ 3%     = $30.00
        // $2,000–$3,000 @ 4%     = $40.00
        // $3,000–$69,050 @ 4.75% = $66,050 × 0.0475 = $3,137.375
        // total = $3,227.375
        // per period = $3,227.375 / 26 = $124.1298… → $124.13
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", exemptions: 2);

        Assert.Equal(124.13m, StateIncome(result));
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Biweekly_StdDedAtMax = $64.57 (wages $1,500); extra $30 → $94.57
        // annual = $1,500 × 26 = $39,000; std ded = $2,550; taxable = $36,450
        // $0–$1,000 @ 2%        = $20.00
        // $1,000–$2,000 @ 3%    = $30.00
        // $2,000–$3,000 @ 4%    = $40.00
        // $3,000–$36,450 @ 4.75% = $33,450 × 0.0475 = $1,588.875
        // total = $1,678.875
        // per period = $1,678.875 / 26 = $64.5721… → $64.57
        // with extra $30 → $94.57
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 30m);

        Assert.Equal(94.57m, StateIncome(result));
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // std ded = max(min($65,000 × 15%, $2,550), $1,600) = max($2,550, $1,600) = $2,550
        // taxable = $65,000 − $2,550 = $62,450
        // $0–$1,000 @ 2%         = $20.00
        // $1,000–$2,000 @ 3%     = $30.00
        // $2,000–$3,000 @ 4%     = $40.00
        // $3,000–$62,450 @ 4.75% = $59,450 × 0.0475 = $2,823.875
        // total = $2,913.875
        // per period = $2,913.875 / 26 = $112.0721… → $112.07
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(112.07m, StateIncome(result));
    }

    // ── Low income — annual wages below standard deduction minimum ────

    [Fact]
    public void Single_Biweekly_IncomeBelowStdDeductionMin_ReturnsZero()
    {
        // annual = $50 × 26 = $1,300
        // std ded = max(min($1,300 × 15%, $2,550), $1,600)
        //         = max($195, $1,600) = $1,600
        // taxable = max(0, $1,300 − $1,600) = $0
        // withholding = $0
        var result = Calculate(GrossWages: 50m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, StateIncome(result));
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, StateIncome(result));
    }

    // ── County income tax ────────────────────────────────────────────
    //
    // Every Maryland employee pays a county rate on the same taxable wages the
    // state brackets use. Rates are Attachment 1 of the Comptroller's 2026
    // Maryland State and Local Income Tax Withholding Information (Feb 4, 2026).
    //
    // Single, $3,000 biweekly, no exemptions:
    //   annual wages       = 3,000 × 26 = 78,000
    //   standard deduction = min(78,000 × 15%, 2,550) = 2,550
    //   annual taxable     = 78,000 − 2,550 = 75,450

    [Fact]
    public void County_FlatRate_AppliesToAnnualTaxableIncome()
    {
        // Montgomery 3.20%: 75,450 × 3.20% = 2,414.40 / 26 = 92.86
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(92.86m, CountyIncome(result));
    }

    [Fact]
    public void County_LowestRate_Worcester()
    {
        // Worcester 2.25%: 75,450 × 2.25% = 1,697.625 → 1,697.63 / 26 = 65.29
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Worcester County");

        Assert.Equal(65.29m, CountyIncome(result));
    }

    [Fact]
    public void County_HighestRate_Kent()
    {
        // Kent 3.30%: 75,450 × 3.30% = 2,489.85 / 26 = 95.76
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Kent County");

        Assert.Equal(95.76m, CountyIncome(result));
    }

    [Fact]
    public void County_NotSupplied_DefaultsToTheHighestLocalRate()
    {
        // The Comptroller directs employers to withhold at the highest local rate
        // (3.30% for 2026) when the employee has not reported a county.
        var withCounty = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Unknown Maryland County");
        var withoutCounty = Calculate(3000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(95.76m, CountyIncome(withoutCounty));
        Assert.Equal(CountyIncome(withCounty), CountyIncome(withoutCounty));
    }

    [Fact]
    public void County_Nonresident_UsesSpecialTwoPointTwoFiveRate()
    {
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Nonresident / Out of State");

        Assert.Equal(65.29m, CountyIncome(result));
    }

    [Fact]
    public void County_AnneArundel_AppliesGraduatedBracketsMarginally()
    {
        // Single brackets: 2.70% to 50,000, then 2.94% to 400,000, then 3.20%.
        //   50,000 × 2.70%          = 1,350.00
        //   (75,450 − 50,000) × 2.94% =   748.23
        //   total 2,098.23 / 26     =    80.70
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Anne Arundel County");

        Assert.Equal(80.70m, CountyIncome(result));
    }

    [Fact]
    public void County_AnneArundel_UsesJointBracketsForMarriedAndHeadOfHousehold()
    {
        // Married annual taxable = 78,000 − 5,100 = 72,900, joint thresholds:
        //   72,900 × 2.70% = 1,968.30 / 26 = 75.70  (all within the first bracket)
        var married = Calculate(3000m, PayFrequency.Biweekly, "Married", county: "Anne Arundel County");
        var headOfHousehold = Calculate(3000m, PayFrequency.Biweekly, "Head of Household", county: "Anne Arundel County");

        Assert.Equal(75.70m, CountyIncome(married));
        Assert.Equal(75.70m, CountyIncome(headOfHousehold));
    }

    [Fact]
    public void County_Frederick_AppliesGraduatedBracketsMarginally()
    {
        // Single brackets: 2.25% to 25,000, 2.75% to 50,000, 2.96% to 150,000, then 3.20%.
        //   25,000 × 2.25%           =   562.50
        //   25,000 × 2.75%           =   687.50
        //   (75,450 − 50,000) × 2.96% =   753.32
        //   total 2,003.32 / 26      =    77.05
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Frederick County");

        Assert.Equal(77.05m, CountyIncome(result));
    }

    [Fact]
    public void County_Frederick_TopBracketAppliesAtHighIncome()
    {
        // Single, $8,000 biweekly: annual taxable = 208,000 − 2,550 = 205,450
        //   25,000 × 2.25%            =   562.50
        //   25,000 × 2.75%            =   687.50
        //   100,000 × 2.96%           = 2,960.00
        //   (205,450 − 150,000) × 3.20% = 1,774.40
        //   total 5,984.40 / 26       =   230.17
        var result = Calculate(8000m, PayFrequency.Biweekly, "Single", county: "Frederick County");

        Assert.Equal(230.17m, CountyIncome(result));
    }

    [Fact]
    public void County_LineIsSeparateFromStateIncomeTax_AndBothSumToWithholding()
    {
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        // Maryland's own tables report a combined figure; the scalar keeps that
        // total while the lines stay itemized.
        Assert.Equal(StateIncome(result) + CountyIncome(result), result.Withholding);
        Assert.Equal(2, result.TaxLines!.Count);
    }

    [Fact]
    public void County_LineNamesTheSelectedCounty()
    {
        var result = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Talbot County");
        var line = result.TaxLines!.Single(l => l.Kind == StateTaxLineKind.CountyIncome);

        Assert.Equal("County Income Tax (Talbot County)", line.Label);
        Assert.Equal(69.65m, line.Amount);
    }

    [Fact]
    public void County_PreTaxDeductionsReduceTheCountyBaseToo()
    {
        // County tax follows the same taxable wages as the state brackets, so a
        // pre-tax deduction lowers both.
        var withDeduction = Calculate(
            3000m, PayFrequency.Biweekly, "Single", preTaxDeductions: 500m, county: "Montgomery County");
        var without = Calculate(3000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.True(CountyIncome(withDeduction) < CountyIncome(without));
    }

    // ── Validation ───────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownCounty_ReturnsError()
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["County"] = "Atlantis County"
        });

        Assert.Contains(errors, e => e.StartsWith("County must be one of:", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_KnownCounty_ReturnsNoCountyError()
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["County"] = "Howard County"
        });

        Assert.DoesNotContain(errors, e => e.StartsWith("County must be one of:", StringComparison.Ordinal));
    }

    [Fact]
    public void Schema_County_DefaultsToUnknownMarylandCounty_AndIsListedFirst()
    {
        // The MAUI picker falls back to the first option when its binding briefly
        // nulls, so the safe default must also be the first entry.
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "County");

        Assert.Equal("Unknown Maryland County", field.DefaultValue);
        Assert.Equal("Unknown Maryland County", field.Options![0]);
        Assert.Equal(26, field.Options!.Count);
        Assert.Contains("Baltimore City", field.Options);
        Assert.Contains("Prince George's County", field.Options);
    }

    // ── Helper ───────────────────────────────────────────────────────

    /// <summary>The state income-tax line alone, excluding the county line.</summary>
    private static decimal StateIncome(StateWithholdingResult result) =>
        result.TaxLines!.Single(l => l.Kind == StateTaxLineKind.StateIncome).Amount;

    /// <summary>The county income-tax line alone.</summary>
    private static decimal CountyIncome(StateWithholdingResult result) =>
        result.TaxLines!.Single(l => l.Kind == StateTaxLineKind.CountyIncome).Amount;

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        int exemptions = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m,
        string? county = null)
    {
        var calc = new MarylandWithholdingCalculator(TestSchemas.Provider, TestMarylandCountyRates.Table);
        var context = new CommonWithholdingContext(
            UsState.MD,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"]          = filingStatus,
            ["Exemptions"]            = exemptions,
            ["AdditionalWithholding"] = additionalWithholding
        };
        if (county is not null)
            values["County"] = county;
        return calc.Calculate(context, values);
    }
}
