using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.California;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class CaliforniaPercentageCalculatorTest
{
    private static CaliforniaPercentageCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ca_method_b_2026.json");
        var json = File.ReadAllText(dataPath);
        return new CaliforniaPercentageCalculator(json);
    }

    // ── Core calculator tests ────────────────────────────────────────

    [Fact]
    public void Monthly_Single_10000_OneAllowance()
    {
        var calc = LoadCalculator();

        // Step 1: Low-income check: $10,000 > $1,575 → not exempt
        // Step 2: Estimated deduction: 0 allowances → $0
        // Step 3: Standard deduction: $476 (monthly, single)
        // TI = $10,000 - $0 - $476 = $9,524
        // Step 4: Per-period brackets (Monthly Single Table 20):
        //   $9,524 falls in bracket over $6,060 @10.23%
        //   Tax = $293.47 + 0.1023 × ($9,524 - $6,060) = $293.47 + $354.3672 = $647.8372
        //   Rounded down (floor) to $647.83
        // Step 5: Credit: 1 allowance → $14.03
        // Withholding = $647.83 - $14.03 = $633.80
        var result = calc.CalculateWithholding(10000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Single, regularAllowances: 1, estimatedDeductionAllowances: 0);

        Assert.Equal(633.80m, result);
    }

    [Fact]
    public void Biweekly_Married_TwoPlus_WithEstimatedDeduction()
    {
        var calc = LoadCalculator();

        // Married with 2 regular allowances → MarriedTwoOrMoreAllowances threshold/deduction
        // Step 1: $4,000 > $1,454 → not exempt
        // Step 2: Estimated deduction: 1 allowance biweekly → $38
        // Step 3: Standard deduction: $439 (biweekly, married 2+)
        // TI = $4,000 - $38 - $439 = $3,523
        // Step 4: Per-period brackets (Biweekly Married Table 27):
        //   $3,523 falls in bracket over $3,188 @6.6%
        //   Tax = $86.46 + 0.066 × ($3,523 - $3,188) = $86.46 + $22.11 = $108.57
        // Step 5: Credit: 2 allowances biweekly → $12.95
        // Withholding = $108.57 - $12.95 = $95.62
        var result = calc.CalculateWithholding(4000m, PayFrequency.Biweekly,
            CaliforniaFilingStatus.Married, regularAllowances: 2, estimatedDeductionAllowances: 1);

        Assert.Equal(95.62m, result);
    }

    [Fact]
    public void Weekly_HeadOfHousehold_ThreeAllowances()
    {
        var calc = LoadCalculator();

        // HOH → UnmarriedHeadOfHousehold threshold/deduction and rate table
        // Step 1: $2,000 > $727 → not exempt
        // Step 2: Estimated deduction: 0 → $0
        // Step 3: Standard deduction: $219 (weekly, HOH)
        // TI = $2,000 - $0 - $219 = $1,781
        // Step 4: Per-period brackets (Weekly HOH Table 25):
        //   $1,781 falls in bracket over $1,612 @8.8%
        //   Tax = $50.85 + 0.088 × ($1,781 - $1,612) = $50.85 + $14.872 = $65.722
        // Step 5: Credit: 3 allowances weekly → $9.71
        // Withholding = $65.722 - $9.71 = $56.012 → $56.01
        var result = calc.CalculateWithholding(2000m, PayFrequency.Weekly,
            CaliforniaFilingStatus.HeadOfHousehold, regularAllowances: 3, estimatedDeductionAllowances: 0);

        Assert.Equal(56.01m, result);
    }

    [Fact]
    public void LowIncomeExemption_Single_Weekly()
    {
        var calc = LoadCalculator();

        // $300 ≤ $363 (weekly single threshold) → exempt
        var result = calc.CalculateWithholding(300m, PayFrequency.Weekly,
            CaliforniaFilingStatus.Single, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void LowIncomeExemption_Married_ZeroAllowances()
    {
        var calc = LoadCalculator();

        // Married with 0 allowances → MarriedZeroOrOneAllowance threshold ($1,575 for monthly)
        // $1,500 ≤ $1,575 → exempt
        var result = calc.CalculateWithholding(1500m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Married, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void LowIncomeExemption_Married_TwoPlus_UsesHighThreshold()
    {
        var calc = LoadCalculator();

        // Married with 2+ allowances → MarriedTwoOrMoreAllowances threshold ($3,149 for monthly)
        // $3,000 ≤ $3,149 → exempt
        var result = calc.CalculateWithholding(3000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Married, regularAllowances: 2, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZero()
    {
        var calc = LoadCalculator();

        var result = calc.CalculateWithholding(0m, PayFrequency.Biweekly,
            CaliforniaFilingStatus.Single, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void NegativeGrossWages_ReturnsZero()
    {
        var calc = LoadCalculator();

        var result = calc.CalculateWithholding(-500m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Single, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Monthly_Married_ZeroAllowances()
    {
        var calc = LoadCalculator();

        // Married with 0 allowances → MarriedZeroOrOneAllowance, married rate brackets
        // Step 1: $2,000 > $1,575 → not exempt
        // Step 2: Estimated deduction: 0
        // Step 3: Standard deduction: $476 (monthly, married 0-1)
        // TI = $2,000 - $476 = $1,524
        // Step 4: Per-period brackets (Monthly Married Table 21):
        //   $1,524 falls in bracket over $0 @1.1%
        //   Tax = $0.00 + 0.011 × ($1,524 - $0) = $16.764
        // Step 5: Credit: 0 → $0
        // Withholding = $16.764 → $16.76
        var result = calc.CalculateWithholding(2000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Married, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(16.76m, result);
    }

    [Fact]
    public void Semimonthly_Single_WithEstimatedDeductions()
    {
        var calc = LoadCalculator();

        // Step 1: $5,000 > $787 → not exempt
        // Step 2: Estimated deduction: 3 allowances semimonthly → $125
        // Step 3: Standard deduction: $238 (semimonthly, single)
        // TI = $5,000 - $125 - $238 = $4,637
        // Step 4: Per-period brackets (Semimonthly Single Table 17):
        //   $4,637 falls in bracket over $3,030 @10.23%
        //   Tax = $146.74 + 0.1023 × ($4,637 - $3,030) = $146.74 + $164.3961 = $311.1361
        //   Rounded down (floor) to $311.13
        // Step 5: Credit: 2 allowances semimonthly → $14.03
        // Withholding = $311.13 - $14.03 = $297.10
        var result = calc.CalculateWithholding(5000m, PayFrequency.Semimonthly,
            CaliforniaFilingStatus.Single, regularAllowances: 2, estimatedDeductionAllowances: 3);

        Assert.Equal(297.10m, result);
    }

    [Fact]
    public void CreditExceedsTax_FloorsAtZero()
    {
        var calc = LoadCalculator();

        // Low income just above exemption, high allowances → credit exceeds tax
        // Monthly, single: $1,600 > $1,575 → not exempt
        // Std deduction: $476 → TI = $1,600 - $476 = $1,124
        // Tax: bracket over $924 @2.2%: $10.16 + 0.022 × ($1,124 - $924) = $10.16 + $4.40 = $14.56
        // Credit: 10 allowances monthly → $140.25
        // Withholding = max(0, $14.56 - $140.25) = $0
        var result = calc.CalculateWithholding(1600m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Single, regularAllowances: 10, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void HighIncome_HitsTopBracket()
    {
        var calc = LoadCalculator();

        // Monthly, single, $100,000 gross, 0 allowances
        // Std deduction: $476; TI = $100,000 - $476 = $99,524
        // Per-period brackets (Monthly Single Table 20):
        //   $99,524 falls in bracket over $83,334 @14.63%
        //   Tax = $9,518.45 + 0.1463 × ($99,524 - $83,334)
        //       = $9,518.45 + 0.1463 × $16,190 = $9,518.45 + $2,368.597 = $11,887.047
        //   Rounded down (floor) to $11,887.04
        // Credit: 0 → Withholding = $11,887.04
        var result = calc.CalculateWithholding(100000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Single, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(11887.04m, result);
    }

    // ── WithholdingCalculator adapter tests ──────────────────────────

    [Fact]
    public void WithholdingCalculator_State_ReturnsCalifornia()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        Assert.Equal(UsState.CA, calc.State);
    }

    [Fact]
    public void WithholdingCalculator_Schema_HasFourFields()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(4, schema.Count);
        Assert.Equal("FilingStatus", schema[0].Key);
        Assert.Equal(StateFieldType.Picker, schema[0].FieldType);
        Assert.Equal("RegularAllowances", schema[1].Key);
        Assert.Equal(StateFieldType.Integer, schema[1].FieldType);
        Assert.Equal("EstimatedDeductionAllowances", schema[2].Key);
        Assert.Equal(StateFieldType.Integer, schema[2].FieldType);
        Assert.Equal("AdditionalWithholding", schema[3].Key);
        Assert.Equal(StateFieldType.Decimal, schema[3].FieldType);
    }

    [Fact]
    public void WithholdingCalculator_Validate_InvalidFilingStatus()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });

        Assert.Single(errors);
    }

    [Fact]
    public void WithholdingCalculator_Validate_ValidFilingStatuses()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" }));
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Married" }));
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Head of Household" }));
    }

    [Fact]
    public void WithholdingCalculator_Calculate_MatchesCoreCalculator()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(10000m, result.TaxableWages);
        Assert.Equal(633.77m, result.Withholding);
        Assert.Equal(130.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void WithholdingCalculator_AdditionalWithholding_IsAdded()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // (633.80 - 0.03) + 25 = 658.77
        Assert.Equal(658.77m, result.Withholding);
        Assert.Equal(130.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void WithholdingCalculator_PreTaxDeductions_ReduceWages()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(8000m, result.TaxableWages);
        // Gross for calc = $8,000, std ded = $476, TI = $7,524
        // Per-period brackets (Monthly Single): bracket over $6,060 @10.23%
        // Tax = $293.47 + 0.1023 × ($7,524 - $6,060) = $293.47 + $149.7672 = $443.2372
        //   Rounded down (floor) to $443.23, minus 0.03 Single workaround = $443.20
        Assert.Equal(443.20m, result.Withholding);
        // SDI is 1.3% of ALL gross wages ($10,000), not reduced wages
        Assert.Equal(130.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void WithholdingCalculator_Sdi_CalculatedOnAllGrossWages()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        // SDI = 1.3% × $5,000 = $65.00
        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(65.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void WithholdingCalculator_Sdi_ZeroGrossWages_ReturnsZero()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.DisabilityInsurance);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void WithholdingCalculator_Sdi_NotReducedByPreTaxDeductions()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        // Gross = $10,000, pre-tax deductions = $3,000
        // SDI should be 1.3% × $10,000 = $130 (ALL wages, not reduced by pre-tax)
        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 3000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // SDI on full $10,000 gross
        Assert.Equal(130.00m, result.DisabilityInsurance);
        // Income tax on reduced $7,000 wages
        Assert.Equal(7000m, result.TaxableWages);
    }

    [Fact]
    public void WithholdingCalculator_SingleWorkaround_Subtracts3Cents()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        // Single filing status: withholding should be reduced by $0.03
        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var singleValues = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var singleResult = calc.Calculate(context, singleValues);

        // Core calculator returns $633.80 for this scenario;
        // Single workaround subtracts $0.03 → $633.77
        Assert.Equal(633.77m, singleResult.Withholding);

        // Married filing status: withholding should NOT be reduced
        var marriedValues = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var marriedResult = calc.Calculate(context, marriedValues);

        // Married withholding is not adjusted
        var expectedMarried = inner.CalculateWithholding(10000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Married, regularAllowances: 1, estimatedDeductionAllowances: 0);
        Assert.Equal(expectedMarried, marriedResult.Withholding);
    }

    [Fact]
    public void WithholdingCalculator_SingleWorkaround_ZeroWithholding_StaysZero()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        // Zero gross wages → $0 withholding; workaround should not make it negative
        var context = new CommonWithholdingContext(
            UsState.CA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DisabilityInsuranceLabel_IsStateDisabilityInsuranceSdi()
    {
        // California SDI should be labeled "State Disability Insurance (SDI)",
        // not the generic "State Disability Insurance" used as the default.
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal("State Disability Insurance (SDI)", result.DisabilityInsuranceLabel);
    }

    // ── Explanation ──────────────────────────────────────────────────

    [Fact]
    public void Explanation_MethodBSteps_MirrorWorksheetTables()
    {
        var calc = LoadCalculator();

        // Same scenario as Monthly_Single_10000_OneAllowance:
        // std deduction $476, TI $9,524, bracket tax $647.83 (10.23% bracket),
        // exemption credit $14.03, withholding $633.80.
        var (withholding, steps) = calc.CalculateWithExplanation(10000m, PayFrequency.Monthly,
            CaliforniaFilingStatus.Single, regularAllowances: 1, estimatedDeductionAllowances: 0);

        Assert.Equal(633.80m, withholding);
        var lowIncomeStep = Assert.Single(steps, s => s.Label == "Low-income exemption test (Table 1)");
        Assert.Contains("continue", lowIncomeStep.Formula);
        var stdDedStep = Assert.Single(steps, s => s.Label == "Less standard deduction (Table 3)");
        Assert.Equal(476m, stdDedStep.Value);
        var taxableStep = Assert.Single(steps, s => s.Label == "Taxable income this period");
        Assert.Equal(9_524m, taxableStep.Value);
        var bracketStep = Assert.Single(steps, s => s.Label == "Tax from the Method B rate table (Table 5)");
        Assert.Equal(647.83m, bracketStep.Value);
        Assert.Contains("10.23", bracketStep.Formula);
        var creditStep = Assert.Single(steps, s => s.Label == "Less exemption allowance credit (Table 4)");
        Assert.Equal(14.03m, creditStep.Value);
        var finalStep = Assert.Single(steps, s => s.Label == "California withholding");
        Assert.Equal(633.80m, finalStep.Value);
    }

    [Fact]
    public void Explanation_LowIncomeExemption_EndsWithExemptStep()
    {
        var calc = LoadCalculator();

        // $300 ≤ $363 (weekly single threshold) → exempt, single explanatory step
        var (withholding, steps) = calc.CalculateWithExplanation(300m, PayFrequency.Weekly,
            CaliforniaFilingStatus.Single, regularAllowances: 0, estimatedDeductionAllowances: 0);

        Assert.Equal(0m, withholding);
        var lowIncomeStep = Assert.Single(steps, s => s.Label == "Low-income exemption test (Table 1)");
        Assert.Contains("no withholding", lowIncomeStep.Formula);
        Assert.DoesNotContain(steps, s => s.Label == "Tax from the Method B rate table (Table 5)");
    }

    [Fact]
    public void Explanation_WrapperSingle_EmitsTableAlignmentStep()
    {
        // The wrapper applies the deliberate 3-cent Single-status adjustment:
        // inner $633.80 − $0.03 = $633.77, shown as its own step.
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA, GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 1,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(633.77m, result.Withholding);
        var adjustmentStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Single filing status table alignment");
        Assert.Equal(633.77m, adjustmentStep.Value);
        Assert.Contains("DE 44", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_WrapperSdiSteps_ShowRateOnGrossWages()
    {
        var inner = LoadCalculator();
        var calc = new CaliforniaWithholdingCalculator(inner, TestSchemas.Provider);

        var context = new CommonWithholdingContext(
            UsState.CA, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["RegularAllowances"] = 0,
            ["EstimatedDeductionAllowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // SDI = 5,000 × 1.3% = $65.00
        Assert.NotNull(result.DisabilityInsuranceSteps);
        var sdiStep = Assert.Single(result.DisabilityInsuranceSteps!, s => s.Label == "State Disability Insurance (1.30%)");
        Assert.Equal(65.00m, sdiStep.Value);
        Assert.Contains("DE 44", result.DisabilityInsuranceReference);
    }
}
