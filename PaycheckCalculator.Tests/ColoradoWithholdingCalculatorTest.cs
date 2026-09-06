using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Colorado;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class ColoradoWithholdingCalculatorTest
{
    private static ColoradoWithholdingCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "co_dr0004_2026.json");
        var json = File.ReadAllText(dataPath);
        return new ColoradoWithholdingCalculator(json, TestSchemas.Provider, TestAssessments.Table);
    }

    [Fact]
    public void State_ReturnsColorado()
    {
        var calc = LoadCalculator();
        Assert.Equal(UsState.CO, calc.State);
    }

    // ── Schema ───────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("Filing Status (from IRS Form W-4 Step 1c)", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("Single or Married Filing Separately", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
    }

    [Fact]
    public void Schema_ContainsNumberOfJobs()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "NumberOfJobs");
        Assert.Equal("Number of Jobs", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("1", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(4, field.Options!.Count);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = LoadCalculator();
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Invalid",
            ["NumberOfJobs"] = "1"
        };

        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_InvalidNumberOfJobs_ReturnsError()
    {
        var calc = LoadCalculator();
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single or Married Filing Separately",
            ["NumberOfJobs"] = "5"
        };

        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Number of Jobs", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = LoadCalculator();
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["NumberOfJobs"] = "2"
        };

        var errors = calc.Validate(values);
        Assert.Empty(errors);
    }

    // ── Flat rate 4.4% with Table 1 allowance ───────────────────────

    [Fact]
    public void Single_1Job_Biweekly_AppliesTable1Allowance()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single or Married Filing Separately",
            ["NumberOfJobs"] = "1"
        };

        var result = calc.Calculate(context, values);

        // Step 1: annual = 5000 * 26 = 130,000
        // Step 2: adjusted = 130,000 - 14,000 = 116,000
        // Step 3: tax = 116,000 * 0.044 = 5,104
        // Step 4: period = 5,104 / 26 = 196.31 (rounded from 196.307692...)
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(196.31m, result.Withholding);
    }

    [Fact]
    public void MarriedJointly_1Job_Biweekly_AppliesTable1Allowance()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly or Qualifying Surviving Spouse",
            ["NumberOfJobs"] = "1"
        };

        var result = calc.Calculate(context, values);

        // Step 1: annual = 5000 * 26 = 130,000
        // Step 2: adjusted = 130,000 - 30,000 = 100,000
        // Step 3: tax = 100,000 * 0.044 = 4,400
        // Step 4: period = 4,400 / 26 = 169.23 (rounded from 169.230769...)
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(169.23m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_2Jobs_Monthly_AppliesTable1Allowance()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 8000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["NumberOfJobs"] = "2"
        };

        var result = calc.Calculate(context, values);

        // Step 1: annual = 8000 * 12 = 96,000
        // Step 2: adjusted = 96,000 - 11,000 = 85,000
        // Step 3: tax = 85,000 * 0.044 = 3,740
        // Step 4: period = 3,740 / 12 = 311.67 (rounded from 311.666...)
        Assert.Equal(8000m, result.TaxableWages);
        Assert.Equal(311.67m, result.Withholding);
    }

    [Fact]
    public void Single_3Jobs_Weekly_AppliesTable1Allowance()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single or Married Filing Separately",
            ["NumberOfJobs"] = "3"
        };

        var result = calc.Calculate(context, values);

        // Step 1: annual = 2000 * 52 = 104,000
        // Step 2: adjusted = 104,000 - 4,500 = 99,500
        // Step 3: tax = 99,500 * 0.044 = 4,378
        // Step 4: period = 4,378 / 52 = 84.19 (rounded from 84.192307...)
        Assert.Equal(2000m, result.TaxableWages);
        Assert.Equal(84.19m, result.Withholding);
    }

    [Fact]
    public void MarriedJointly_4Jobs_Semimonthly_AppliesTable1Allowance()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly or Qualifying Surviving Spouse",
            ["NumberOfJobs"] = "4"
        };

        var result = calc.Calculate(context, values);

        // Step 1: annual = 4000 * 24 = 96,000
        // Step 2: adjusted = 96,000 - 7,500 = 88,500
        // Step 3: tax = 88,500 * 0.044 = 3,894
        // Step 4: period = 3,894 / 24 = 162.25
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(162.25m, result.Withholding);
    }

    // ── Default values (no explicit inputs) ─────────────────────────

    [Fact]
    public void DefaultInputs_UsesSingle1Job()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // Defaults: Single or Married Filing Separately, 1 Job
        // Step 1: annual = 5000 * 26 = 130,000
        // Step 2: adjusted = 130,000 - 14,000 = 116,000
        // Step 3: tax = 116,000 * 0.044 = 5,104
        // Step 4: period = 5,104 / 26 = 196.31
        Assert.Equal(196.31m, result.Withholding);
    }

    // ── Allowance exceeds annual wages ──────────────────────────────

    [Fact]
    public void AllowanceExceedsAnnualWages_FloorsAtZero()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly or Qualifying Surviving Spouse",
            ["NumberOfJobs"] = "1"
        };

        var result = calc.Calculate(context, values);

        // annual = 1000 * 1 = 1,000
        // adjusted = max(0, 1,000 - 30,000) = 0
        // tax = 0
        Assert.Equal(1000m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── FAMLI (Family and Medical Leave Insurance) ──────────────────
    //
    // Colorado FAMLI 2026: the total premium is 0.88% of wages, split evenly, so
    // the employee pays 0.44% on wages up to the $184,500 federal Social Security
    // wage cap.
    // https://famli.colorado.gov/individuals-and-families/how-famli-works/premium-and-benefits-calculator

    [Fact]
    public void Famli_CalculatedOnGrossWages()
    {
        // 5,000 × 0.44% = 22.00
        var result = CalculateFamli(grossWages: 5000m, frequency: PayFrequency.Biweekly);

        Assert.Equal(22.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Famli_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        // The premium follows gross wages (10,000), not reduced wages (8,000):
        // 10,000 × 0.44% = 44.00
        var result = CalculateFamli(
            grossWages: 10_000m, frequency: PayFrequency.Monthly, preTaxDeductions: 2000m);

        Assert.Equal(44.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Famli_HasItsOwnLabel_NotTheGenericDisabilityHeading()
    {
        var result = CalculateFamli(grossWages: 5000m, frequency: PayFrequency.Biweekly);

        Assert.Equal("Family and Medical Leave Insurance (FAMLI)", result.DisabilityInsuranceLabel);
    }

    [Fact]
    public void Famli_StopsAtSocialSecurityWageCap()
    {
        // $180,000 earned leaves $4,500 of the $184,500 cap: 4,500 × 0.44% = 19.80
        var result = CalculateFamli(
            grossWages: 10_000m, frequency: PayFrequency.Monthly, ytdStateWages: 180_000m);

        Assert.Equal(19.80m, result.DisabilityInsurance);
    }

    [Fact]
    public void Famli_AtWageCap_WithholdsNothingFurther()
    {
        var result = CalculateFamli(
            grossWages: 10_000m, frequency: PayFrequency.Monthly, ytdStateWages: 184_500m);

        Assert.Equal(0m, result.DisabilityInsurance);
    }

    [Fact]
    public void Famli_RoundsAwayFromZero()
    {
        // 125 × 0.44% = 0.55 exactly; 1,125 × 0.44% = 4.95.
        // 625 × 0.44% = 2.75; 2,625 × 0.44% = 11.55.
        // 78.75 × 0.44% = 0.3465 → 0.35.
        var result = CalculateFamli(grossWages: 78.75m, frequency: PayFrequency.Weekly);

        Assert.Equal(0.35m, result.DisabilityInsurance);
    }

    private static StateWithholdingResult CalculateFamli(
        decimal grossWages,
        PayFrequency frequency,
        decimal preTaxDeductions = 0m,
        decimal ytdStateWages = 0m)
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: grossWages,
            PayPeriod: frequency,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions,
            YtdStateWages: ytdStateWages);

        return calc.Calculate(context, new StateInputValues());
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single or Married Filing Separately",
            ["NumberOfJobs"] = "1"
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 5000 - 1000 = 4000
        // Step 1: annual = 4000 * 26 = 104,000
        // Step 2: adjusted = 104,000 - 14,000 = 90,000
        // Step 3: tax = 90,000 * 0.044 = 3,960
        // Step 4: period = 3,960 / 26 = 152.31 (rounded from 152.307692...)
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(152.31m, result.Withholding);
    }

    // ── Additional withholding ──────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single or Married Filing Separately",
            ["NumberOfJobs"] = "1",
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // 196.31 + 25 = 221.31
        Assert.Equal(221.31m, result.Withholding);
    }

    // ── Zero wages edge case ────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Deductions exceed gross ─────────────────────────────────────

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = LoadCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
        // FAMLI still applies to gross wages
        // FAMLI = 1000 × 0.44% = 4.40
        Assert.Equal(4.40m, result.DisabilityInsurance);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_HeadOfHousehold_3Jobs_WithDeductionsAndFamli()
    {
        var calc = LoadCalculator();

        // Biweekly employee: $4000 gross, $500 pre-tax, Head of Household, 3 jobs
        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["NumberOfJobs"] = "3",
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 4000 - 500 = 3500
        // Step 1: annual = 3500 * 26 = 91,000
        // Step 2: adjusted = 91,000 - 7,500 = 83,500
        // Step 3: tax = 83,500 * 0.044 = 3,674
        // Step 4: period = 3,674 / 26 = 141.31 (rounded from 141.307692...)
        // withholding = 141.31 + 10 = 151.31
        Assert.Equal(3500m, result.TaxableWages);
        Assert.Equal(151.31m, result.Withholding);

        // FAMLI = 4000 × 0.44% = 17.60
        Assert.Equal(17.60m, result.DisabilityInsurance);
    }
}
