using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Maryland;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Maryland (MD) state and county income tax withholding.
/// Maryland uses the dedicated <see cref="MarylandWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the percentage method of the
/// Comptroller of Maryland's 2026 Employer Withholding Guide (revised December
/// 2025), which works one payroll period at a time and never annualizes:
///   taxable income = max(0, per-period wages − pre-tax deductions
///                            − standardDeduction(period)
///                            − exemptions × exemptionValue(period))
///   state tax      = period rate schedule applied to taxable income
///   county tax     = county rate applied to the same taxable income
/// Each line is rounded separately, then Form MW507 extra withholding is added.
///
/// Guide page 10 publishes these per-period allowances:
///   Payroll period   One exemption   Standard deduction   No withholding below
///   Daily            $     8.77      $     9.31           $    13.70
///   Weekly           $    61.54      $    65.38           $    96.00
///   Bi-weekly        $   123.08      $   130.76           $   192.00
///   Semi-monthly     $   133.33      $   141.66           $   208.00
///   Monthly          $   266.67      $   283.33           $   417.00
///   Quarterly        $   800.00      $   850.00           $ 1,250.00
///   Annually         $ 3,200.00      $ 3,400.00           $ 5,000.00
///
/// The withholding rate schedule starts at the 4.75% statutory minimum — the
/// 2%/3%/4% brackets of Form 502 may not be used for withholding — and its
/// annual ceilings are scaled to the payroll period:
///   Single / MFS / dependent:
///     4.75% to $100,000 | 5.00% to $125,000 | 5.25% to $150,000
///     5.50% to $250,000 | 5.75% to $500,000 | 6.25% to $1,000,000
///     6.50% above
///   Married filing jointly / Head of Household:
///     4.75% to $150,000 | 5.00% to $175,000 | 5.25% to $225,000
///     5.50% to $300,000 | 5.75% to $600,000 | 6.25% to $1,200,000
///     6.50% above
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

    // ── The Comptroller's own worked shape ───────────────────────────

    [Fact]
    public void Single_Biweekly_CarrollCounty_MatchesThePublishedPercentageMethod()
    {
        // 80 hours at $20.00, bi-weekly, single, Carroll County, no exemptions —
        // the reference case for this formula.
        //   taxable income = $1,600.00 − $130.76 = $1,469.24
        //   state  = $1,469.24 × 4.75% = $69.78890  → $69.79
        //   county = $1,469.24 × 3.03% = $44.517972 → $44.52
        // The guide's printed 3.05% table (page 31) reaches the same total through
        // its combined 7.80% first-band rate: 4.75% state + the 3.05% rate group
        // that Carroll's actual 3.03% rounds up into.
        var result = Calculate(1_600m, PayFrequency.Biweekly, "Single", county: "Carroll County");

        Assert.Equal(1_600m, result.TaxableWages);
        Assert.Equal(69.79m, StateIncome(result));
        Assert.Equal(44.52m, CountyIncome(result));
    }

    [Fact]
    public void Single_Biweekly_StateAndCountyReproduceThePublishedTableRate()
    {
        // Montgomery's 3.20% is itself one of the guide's ten rate groups, so its
        // combined first-band rate is printed as 7.95% (page 37).
        //   state  = $1,469.24 × 4.75% = $69.79
        //   county = $1,469.24 × 3.20% = $47.01568 → $47.02
        // The table's own combined figure is $1,469.24 × 7.95% = $116.80; showing
        // the two lines separately rounds each, so the sum is a cent higher. That
        // is the same split ADP and PaycheckCity report.
        var result = Calculate(1_600m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(69.79m, StateIncome(result));
        Assert.Equal(47.02m, CountyIncome(result));
        Assert.Equal(116.81m, result.Withholding);
    }

    // ── Minimum withholding rate ─────────────────────────────────────

    [Fact]
    public void Single_Biweekly_MinimumRateAppliesFromTheFirstDollar()
    {
        // Maryland law does not permit a withholding rate below 4.75%, so the
        // 2%/3%/4% brackets of the annual return never apply here.
        //   taxable = $3,000.00 − $130.76 = $2,869.24
        //   state   = $2,869.24 × 4.75% = $136.28890 → $136.29
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(136.29m, StateIncome(result));
    }

    [Fact]
    public void Single_Annual_StandardDeductionIsFlatThirtyFourHundred()
    {
        // The 2025 Legislative Session replaced the variable 15%-of-wages
        // standard deduction with a flat $3,400 for the percentage method.
        //   taxable = $5,000.00 − $3,400.00 = $1,600.00
        //   state   = $1,600.00 × 4.75% = $76.00
        var result = Calculate(5_000m, PayFrequency.Annual, "Single", county: "Montgomery County");

        Assert.Equal(76.00m, StateIncome(result));
    }

    // ── Per-period allowances ────────────────────────────────────────

    [Fact]
    public void Single_Weekly_UsesTheWeeklyAllowance()
    {
        // taxable = $1,000.00 − $65.38 = $934.62
        // state   = $934.62 × 4.75% = $44.394450 → $44.39
        var result = Calculate(1_000m, PayFrequency.Weekly, "Single", county: "Montgomery County");

        Assert.Equal(44.39m, StateIncome(result));
    }

    [Fact]
    public void Single_Semimonthly_UsesTheSemimonthlyAllowance()
    {
        // taxable = $2,500.00 − $141.66 = $2,358.34
        // state   = $2,358.34 × 4.75% = $112.021150 → $112.02
        var result = Calculate(2_500m, PayFrequency.Semimonthly, "Single", county: "Montgomery County");

        Assert.Equal(112.02m, StateIncome(result));
    }

    [Fact]
    public void Single_Quarterly_UsesTheQuarterlyAllowance()
    {
        // taxable = $20,000.00 − $850.00 = $19,150.00
        // state   = $19,150.00 × 4.75% = $909.625 → $909.63
        var result = Calculate(20_000m, PayFrequency.Quarterly, "Single", county: "Montgomery County");

        Assert.Equal(909.63m, StateIncome(result));
    }

    [Fact]
    public void Single_Daily_UsesTheDailyAllowance()
    {
        // taxable = $200.00 − $9.31 = $190.69
        // state   = $190.69 × 4.75% = $9.057775 → $9.06
        var result = Calculate(200m, PayFrequency.Daily, "Single", county: "Montgomery County");

        Assert.Equal(9.06m, StateIncome(result));
    }

    [Fact]
    public void Single_Daily_SecondBracketUsesTheGuidesThreeHundredSixtyFourDayDivisor()
    {
        // The guide's daily allowances are annual ÷ 365, but its daily bracket
        // thresholds are annual ÷ 364 (52 weeks × 7 days) — $100,000 ÷ 364 =
        // $274.73, printed as $275 on page 31. Both are reproduced as published.
        //   taxable = $300.00 − $9.31 = $290.69
        //   $0–$274.725274…  @ 4.75% = $13.049450
        //   remainder $15.964725… @ 5.00% = $0.798236
        //   state = $13.847686 → $13.85
        var result = Calculate(300m, PayFrequency.Daily, "Single", county: "Montgomery County");

        Assert.Equal(13.85m, StateIncome(result));
    }

    [Fact]
    public void FiftyThreeWeekYear_UsesTheSameWeeklyAllowanceAsAnOrdinaryWeek()
    {
        // A 53rd weekly paycheck is still a weekly payroll period, so it takes the
        // weekly row of the guide's table.
        var weekly = Calculate(1_000m, PayFrequency.Weekly, "Single", county: "Montgomery County");
        var fiftyThree = Calculate(1_000m, PayFrequency.Weekly53, "Single", county: "Montgomery County");

        Assert.Equal(44.39m, StateIncome(fiftyThree));
        Assert.Equal(StateIncome(weekly), StateIncome(fiftyThree));
    }

    // ── Filing status ────────────────────────────────────────────────

    [Fact]
    public void Single_Monthly_UpperBrackets()
    {
        // taxable = $12,000.00 − $283.33 = $11,716.67; monthly ceilings are the
        // annual ones ÷ 12: $8,333.33…, $10,416.66…, $12,500.
        //   $0–$8,333.333333…       @ 4.75% = $395.833333
        //   to $10,416.666666…      @ 5.00% = $104.166667
        //   remainder $1,300.003333 @ 5.25% =  $68.250175
        //   state = $568.250175 → $568.25
        var result = Calculate(12_000m, PayFrequency.Monthly, "Single", county: "Montgomery County");

        Assert.Equal(568.25m, StateIncome(result));
    }

    [Fact]
    public void Married_Monthly_StaysInTheMinimumRateBandLongerThanSingle()
    {
        // The flat standard deduction is the same for every status now, so married
        // and single differ only where the wider joint brackets bite: the joint
        // 4.75% band runs to $150,000 ($12,500 monthly), so all of the taxable
        // income stays in it.
        //   taxable = $12,000.00 − $283.33 = $11,716.67
        //   state   = $11,716.67 × 4.75% = $556.541825 → $556.54
        var result = Calculate(12_000m, PayFrequency.Monthly, "Married", county: "Montgomery County");

        Assert.Equal(556.54m, StateIncome(result));
    }

    [Fact]
    public void HeadOfHousehold_Monthly_UsesTheJointSchedule()
    {
        // The guide's JOINT column covers head of household and qualifying
        // surviving spouse as well as married filing jointly.
        var headOfHousehold = Calculate(12_000m, PayFrequency.Monthly, "Head of Household", county: "Montgomery County");
        var married = Calculate(12_000m, PayFrequency.Monthly, "Married", county: "Montgomery County");

        Assert.Equal(556.54m, StateIncome(headOfHousehold));
        Assert.Equal(StateIncome(married), StateIncome(headOfHousehold));
    }

    [Fact]
    public void Single_Monthly_HighIncome_ReachesTheTopBracket()
    {
        // taxable = $100,000.00 − $283.33 = $99,716.67; monthly ceilings are the
        // annual ones ÷ 12.
        //   $0–$8,333.333333…        @ 4.75% =   $395.833333
        //   to $10,416.666666…       @ 5.00% =   $104.166667
        //   to $12,500               @ 5.25% =   $109.375000
        //   to $20,833.333333…       @ 5.50% =   $458.333333
        //   to $41,666.666666…       @ 5.75% = $1,197.916667
        //   to $83,333.333333…       @ 6.25% = $2,604.166667
        //   remainder $16,383.336666 @ 6.50% = $1,064.916883
        //   state = $5,934.708550 → $5,934.71
        var result = Calculate(100_000m, PayFrequency.Monthly, "Single", county: "Montgomery County");

        Assert.Equal(5_934.71m, StateIncome(result));
    }

    // ── Exemptions ───────────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_TwoExemptions_UseThePerPeriodExemptionValue()
    {
        // taxable = $3,000.00 − $130.76 − (2 × $123.08) = $2,623.08
        // state   = $2,623.08 × 4.75% = $124.596300 → $124.60
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single",
            exemptions: 2, county: "Montgomery County");

        Assert.Equal(124.60m, StateIncome(result));
    }

    [Fact]
    public void Exemptions_CannotDriveTaxableIncomeBelowZero()
    {
        // $200.00 − $130.76 − $123.08 = −$53.84, floored at $0.
        var result = Calculate(200m, PayFrequency.Biweekly, "Single",
            exemptions: 1, county: "Montgomery County");

        Assert.Equal(0m, StateIncome(result));
        Assert.Equal(0m, CountyIncome(result));
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToTheStateLineOnly()
    {
        // taxable = $1,500.00 − $130.76 = $1,369.24
        // state   = $1,369.24 × 4.75% = $65.038900 → $65.04, plus $30.00 = $95.04
        // county  = $1,369.24 × 3.20% = $43.815680 → $43.82, unchanged
        var result = Calculate(1_500m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 30m, county: "Montgomery County");

        Assert.Equal(95.04m, StateIncome(result));
        Assert.Equal(43.82m, CountyIncome(result));
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndBothLines()
    {
        // gross $3,000, pre-tax $500 → state taxable wages = $2,500
        //   taxable = $2,500.00 − $130.76 = $2,369.24
        //   state   = $2,369.24 × 4.75% = $112.538900 → $112.54
        //   county  = $2,369.24 × 3.20% =  $75.815680 →  $75.82
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m, county: "Montgomery County");

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(112.54m, StateIncome(result));
        Assert.Equal(75.82m, CountyIncome(result));
    }

    // ── "Do not withhold" floor ───────────────────────────────────────

    [Fact]
    public void Biweekly_WagesBelowTheTableFloor_WithholdNothingAtAll()
    {
        // Every published table carries "DO NOT WITHHOLD ON GROSS WAGES LESS THAN
        // $192.00" for a bi-weekly period — the $5,000 annual threshold prorated.
        var result = Calculate(191.99m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(0m, StateIncome(result));
        Assert.Equal(0m, CountyIncome(result));
    }

    [Fact]
    public void Biweekly_WagesAtTheTableFloor_AreWithheldNormally()
    {
        // taxable = $192.00 − $130.76 = $61.24
        //   state  = $61.24 × 4.75% = $2.908900 → $2.91
        //   county = $61.24 × 3.20% = $1.959680 → $1.96
        var result = Calculate(192m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(2.91m, StateIncome(result));
        Assert.Equal(1.96m, CountyIncome(result));
    }

    [Fact]
    public void Daily_WagesBelowTheDailyFloor_WithholdNothing()
    {
        var result = Calculate(13.69m, PayFrequency.Daily, "Single", county: "Montgomery County");

        Assert.Equal(0m, StateIncome(result));
        Assert.Equal(0m, CountyIncome(result));
    }

    [Fact]
    public void BelowTheFloor_AdditionalWithholdingIsStillHonored()
    {
        // The floor suppresses the computed tax, not an amount the employee asked
        // for on Form MW507.
        var result = Calculate(150m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 25m, county: "Montgomery County");

        Assert.Equal(25m, StateIncome(result));
        Assert.Equal(0m, CountyIncome(result));
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(0m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, StateIncome(result));
        Assert.Equal(0m, CountyIncome(result));
    }

    // ── County income tax ────────────────────────────────────────────
    //
    // Every Maryland employee pays a county rate on the same taxable income the
    // state schedule uses. Rates are the 2026 Withholding Tax Facts county
    // listing. The county's actual rate applies, not the rounded-up rate group
    // the printed tables bucket it into.
    //
    // Single, $3,000 bi-weekly, no exemptions:
    //   taxable income = $3,000.00 − $130.76 = $2,869.24

    [Fact]
    public void County_FlatRate_AppliesToThePeriodTaxableIncome()
    {
        // Montgomery 3.20%: $2,869.24 × 3.20% = $91.815680 → $91.82
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        Assert.Equal(91.82m, CountyIncome(result));
    }

    [Fact]
    public void County_LowestRate_Worcester()
    {
        // Worcester 2.25%: $2,869.24 × 2.25% = $64.557900 → $64.56
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Worcester County");

        Assert.Equal(64.56m, CountyIncome(result));
    }

    [Fact]
    public void County_HighestRate_Kent()
    {
        // Kent 3.30%: $2,869.24 × 3.30% = $94.684920 → $94.68
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Kent County");

        Assert.Equal(94.68m, CountyIncome(result));
    }

    [Fact]
    public void County_NotSupplied_DefaultsToTheHighestLocalRate()
    {
        // The Comptroller directs employers to withhold at the highest local rate
        // (3.30% for 2026) when the employee has not reported a county.
        var withCounty = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Unknown Maryland County");
        var withoutCounty = Calculate(3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(94.68m, CountyIncome(withoutCounty));
        Assert.Equal(CountyIncome(withCounty), CountyIncome(withoutCounty));
    }

    [Fact]
    public void County_Nonresident_UsesSpecialTwoPointTwoFiveRate()
    {
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Nonresident / Out of State");

        Assert.Equal(64.56m, CountyIncome(result));
    }

    [Fact]
    public void County_AnneArundel_AppliesGraduatedBracketsScaledToThePeriod()
    {
        // Single brackets: 2.70% to $50,000, 2.94% to $400,000, then 3.20%.
        // Bi-weekly, the first ceiling is $50,000 ÷ 26 = $1,923.076923…
        //   $1,923.076923… × 2.70% = $51.923077
        //   remainder $946.163077 × 2.94% = $27.817194
        //   total $79.740271 → $79.74
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Anne Arundel County");

        Assert.Equal(79.74m, CountyIncome(result));
    }

    [Fact]
    public void County_AnneArundel_UsesJointBracketsForMarriedAndHeadOfHousehold()
    {
        // Joint first ceiling is $75,000 ÷ 26 = $2,884.615385, above the whole
        // taxable income, so all of it stays at 2.70%.
        //   $2,869.24 × 2.70% = $77.469480 → $77.47
        var married = Calculate(3_000m, PayFrequency.Biweekly, "Married", county: "Anne Arundel County");
        var headOfHousehold = Calculate(3_000m, PayFrequency.Biweekly, "Head of Household", county: "Anne Arundel County");

        Assert.Equal(77.47m, CountyIncome(married));
        Assert.Equal(77.47m, CountyIncome(headOfHousehold));
    }

    [Fact]
    public void County_Frederick_AppliesGraduatedBracketsScaledToThePeriod()
    {
        // Single brackets: 2.25% to $25,000, 2.75% to $50,000, 2.96% to $150,000,
        // then 3.20%. Bi-weekly ceilings are $961.538462 and $1,923.076923.
        //   $961.538462   × 2.25% = $21.634615
        //   $961.538461   × 2.75% = $26.442308
        //   remainder $946.163077 × 2.96% = $28.006427
        //   total $76.083350 → $76.08
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Frederick County");

        Assert.Equal(76.08m, CountyIncome(result));
    }

    [Fact]
    public void County_Frederick_TopBracketAppliesAtHighIncome()
    {
        // Single, $8,000 bi-weekly: taxable = $8,000.00 − $130.76 = $7,869.24,
        // above the $150,000 ÷ 26 = $5,769.230769 ceiling.
        //   $961.538462     × 2.25% =  $21.634615
        //   $961.538461     × 2.75% =  $26.442308
        //   $3,846.153846   × 2.96% = $113.846154
        //   remainder $2,100.009231 × 3.20% = $67.200295
        //   total $229.123372 → $229.12
        var result = Calculate(8_000m, PayFrequency.Biweekly, "Single", county: "Frederick County");

        Assert.Equal(229.12m, CountyIncome(result));
    }

    [Fact]
    public void County_LineIsSeparateFromStateIncomeTax_AndBothSumToWithholding()
    {
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

        // Maryland's own tables report a combined figure; the scalar keeps that
        // total while the lines stay itemized.
        Assert.Equal(StateIncome(result) + CountyIncome(result), result.Withholding);
        Assert.Equal(2, result.TaxLines!.Count);
    }

    [Fact]
    public void County_LineNamesTheSelectedCounty()
    {
        // Talbot 2.40%: $2,869.24 × 2.40% = $68.861760 → $68.86
        var result = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Talbot County");
        var line = result.TaxLines!.Single(l => l.Kind == StateTaxLineKind.CountyIncome);

        Assert.Equal("County Income Tax (Talbot County)", line.Label);
        Assert.Equal(68.86m, line.Amount);
    }

    [Fact]
    public void County_PreTaxDeductionsReduceTheCountyBaseToo()
    {
        // County tax follows the same taxable income as the state schedule, so a
        // pre-tax deduction lowers both.
        var withDeduction = Calculate(
            3_000m, PayFrequency.Biweekly, "Single", preTaxDeductions: 500m, county: "Montgomery County");
        var without = Calculate(3_000m, PayFrequency.Biweekly, "Single", county: "Montgomery County");

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
