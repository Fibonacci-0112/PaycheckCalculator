using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Connecticut;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class ConnecticutWithholdingCalculatorTest
{
    private static ConnecticutWithholdingCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "connecticut_withholding_2026.json");
        var json = File.ReadAllText(dataPath);
        return new ConnecticutWithholdingCalculator(json, TestSchemas.Provider, TestAssessments.Table);
    }

    [Fact]
    public void State_ReturnsConnecticut()
    {
        var calc = LoadCalculator();
        Assert.Equal(UsState.CT, calc.State);
    }

    // ── Schema ───────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsWithholdingCode()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "WithholdingCode");
        Assert.Equal("Withholding Code (CT-W4 Line 1)", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal("Code A", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(7, field.Options!.Count);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding (CT-W4 Line 2)", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
    }

    [Fact]
    public void Schema_ContainsReducedWithholding()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "ReducedWithholding");
        Assert.Equal("Reduced Withholding (CT-W4 Line 3)", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidWithholdingCode_ReturnsError()
    {
        var calc = LoadCalculator();
        var values = new StateInputValues { ["WithholdingCode"] = "Code X" };

        var errors = calc.Validate(values);
        Assert.Single(errors);
        Assert.Contains("Withholding Code", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = LoadCalculator();
        var values = new StateInputValues { ["WithholdingCode"] = "Code B" };

        var errors = calc.Validate(values);
        Assert.Empty(errors);
    }

    // ── Code A scenarios ────────────────────────────────────────────

    [Fact]
    public void CodeA_Biweekly_3000_HighIncomeNoExemption()
    {
        // S = 3000 * 26 = 78,000
        // Table A: exemption = 0 (S > 35,000)
        // TI = 78,000
        // Table B: 2000 + 0.055 * (78000 - 50000) = 3540
        // Table C: add_back = 250 (S > 72,750)
        // Table D: recapture = 0 (S <= 105,000)
        // Pre-credit = 3540 + 250 + 0 = 3790
        // Table E: credit = 0.00 (S > 52,500)
        // Annual = 3790 * 1.00 = 3790
        // Per-period = 3790 / 26 = 145.77
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(145.77m, result.Withholding);
    }

    [Fact]
    public void CodeA_Annual_20000_LowIncomeWithExemptionAndCredit()
    {
        // S = 20,000 * 1 = 20,000
        // Table A: exemption = 12,000 (S <= 24,000)
        // TI = 8,000
        // Table B: 0 + 0.02 * 8000 = 160
        // Table C: add_back = 0 (S <= 50,250)
        // Table D: recapture = 0 (S <= 105,000)
        // Pre-credit = 160
        // Table E: credit = 0.35 (S > 18,500 and <= 20,000)
        // Annual = 160 * 0.65 = 104
        // Per-period = 104 / 1 = 104.00
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 20000m,
            PayPeriod: PayFrequency.Annual, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        Assert.Equal(20000m, result.TaxableWages);
        Assert.Equal(104.00m, result.Withholding);
    }

    [Fact]
    public void CodeA_Biweekly_10000_HighIncomeAddBackAndRecapture()
    {
        // S = 10000 * 26 = 260,000
        // Table A: exemption = 0 (S > 35,000)
        // TI = 260,000
        // Table B: 14000 + 0.069 * (260000 - 250000) = 14690
        // Table C: add_back = 250 (S > 72,750)
        // Table D: recapture = 1330 (S > 255,000 and <= 260,000)
        // Pre-credit = 14690 + 250 + 1330 = 16270
        // Table E: credit = 0.00 (S > 52,500)
        // Annual = 16270
        // Per-period = 16270 / 26 = 625.77
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 10000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        Assert.Equal(10000m, result.TaxableWages);
        Assert.Equal(625.77m, result.Withholding);
    }

    // ── Code B ──────────────────────────────────────────────────────

    [Fact]
    public void CodeB_Monthly_4000_ExemptionAndCredit()
    {
        // S = 4000 * 12 = 48,000
        // Table A: exemption = 9,000 (S > 47,000 and <= 48,000)
        // TI = 39,000
        // Table B: 320 + 0.045 * (39000 - 16000) = 1355
        // Table C: add_back = 0 (S <= 78,500)
        // Table D: recapture = 0 (S <= 168,000)
        // Pre-credit = 1355
        // Table E: credit = 0.10 (S > 46,000 and <= 74,000)
        // Annual = 1355 * 0.90 = 1219.50
        // Per-period = 1219.50 / 12 = 101.63
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 4000m,
            PayPeriod: PayFrequency.Monthly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code B" };

        var result = calc.Calculate(context, values);

        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(101.63m, result.Withholding);
    }

    // ── Code C ──────────────────────────────────────────────────────

    [Fact]
    public void CodeC_Biweekly_2500_ExemptionAndCredit()
    {
        // S = 2500 * 26 = 65,000
        // Table A: exemption = 7,000 (S > 64,000 and <= 65,000)
        // TI = 58,000
        // Table B: 400 + 0.045 * (58000 - 20000) = 2110
        // Table C: add_back = 0 (S <= 100,500)
        // Table D: recapture = 0 (S <= 210,000)
        // Pre-credit = 2110
        // Table E: credit = 0.10 (S > 52,000 and <= 96,000)
        // Annual = 2110 * 0.90 = 1899
        // Per-period = 1899 / 26 = 73.04
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 2500m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code C" };

        var result = calc.Calculate(context, values);

        Assert.Equal(2500m, result.TaxableWages);
        Assert.Equal(73.04m, result.Withholding);
    }

    // ── Code D (no exemption, no credit, same tables as A) ──────────

    [Fact]
    public void CodeD_Biweekly_5000_NoExemptionNoCredit()
    {
        // S = 5000 * 26 = 130,000
        // Table A: exemption = 0 (Code D always 0)
        // TI = 130,000
        // Table B (same_as_A): 4750 + 0.06 * (130000 - 100000) = 6550
        // Table C (same_as_A): add_back = 250 (S > 72,750)
        // Table D (same_as_A): recapture = 125 (S > 125,000 and <= 130,000)
        // Pre-credit = 6550 + 250 + 125 = 6925
        // Table E: credit = 0.00 (Code D always 0)
        // Annual = 6925
        // Per-period = 6925 / 26 = 266.35
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code D" };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(266.35m, result.Withholding);
    }

    // ── Code E (no withholding unless additional) ───────────────────

    [Fact]
    public void CodeE_NoWithholding()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code E" };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
        Assert.Contains("no Connecticut withholding", result.Description!);
    }

    [Fact]
    public void CodeE_WithAdditionalWithholding()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingCode"] = "Code E",
            ["AdditionalWithholding"] = 50m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(50m, result.Withholding);
    }

    // ── Code F ──────────────────────────────────────────────────────

    [Fact]
    public void CodeF_Weekly_1500_NoExemptionNoCredit()
    {
        // S = 1500 * 52 = 78,000
        // Table A: exemption = 0 (S > 44,000)
        // TI = 78,000
        // Table B (same_as_A): 2000 + 0.055 * (78000 - 50000) = 3540
        // Table C (Code F): add_back = 125 (S > 76,500 and <= 81,500)
        // Table D (same_as_A): recapture = 0 (S <= 105,000)
        // Pre-credit = 3540 + 125 = 3665
        // Table E: credit = 0.00 (S > 64,500)
        // Annual = 3665
        // Per-period = 3665 / 52 = 70.48
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 1500m,
            PayPeriod: PayFrequency.Weekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code F" };

        var result = calc.Calculate(context, values);

        Assert.Equal(1500m, result.TaxableWages);
        Assert.Equal(70.48m, result.Withholding);
    }

    [Fact]
    public void CodeF_Weekly_500_ExemptionAndCredit()
    {
        // S = 500 * 52 = 26,000
        // Table A: exemption = 15,000 (S <= 30,000)
        // TI = 11,000
        // Table B (same_as_A): 200 + 0.045 * (11000 - 10000) = 245
        // Table C (Code F): add_back = 0 (S <= 56,500)
        // Table D (same_as_A): recapture = 0 (S <= 105,000)
        // Pre-credit = 245
        // Table E: credit = 0.25 (S > 25,500 and <= 26,000)
        // Annual = 245 * 0.75 = 183.75
        // Per-period = 183.75 / 52 = 3.53
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code F" };

        var result = calc.Calculate(context, values);

        Assert.Equal(500m, result.TaxableWages);
        Assert.Equal(3.53m, result.Withholding);
    }

    // ── No Form CT-W4 (flat 6.99% of taxable wages) ───────────────────

    [Fact]
    public void NoFormCTW4_Biweekly_5000_FlatRate()
    {
        // No Form CT-W4: flat 6.99% of taxable wages per period
        // 5000 * 0.0699 = 349.50
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "No Form CT-W4" };

        var result = calc.Calculate(context, values);

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(349.50m, result.Withholding);
        Assert.Contains("6.99%", result.Description!);
    }

    [Fact]
    public void NoFormCTW4_WithAdditionalWithholding()
    {
        // 5000 * 0.0699 = 349.50 + 25 additional = 374.50
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingCode"] = "No Form CT-W4",
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(374.50m, result.Withholding);
    }

    [Fact]
    public void NoFormCTW4_WithPreTaxDeductions()
    {
        // taxable wages = 5000 - 1000 = 4000
        // 4000 * 0.0699 = 279.60
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);
        var values = new StateInputValues { ["WithholdingCode"] = "No Form CT-W4" };

        var result = calc.Calculate(context, values);

        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(279.60m, result.Withholding);
    }

    [Fact]
    public void NoFormCTW4_ZeroWages_ReturnsZero()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "No Form CT-W4" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void NoFormCTW4_DiffersFromCodeD()
    {
        // No Form CT-W4 (flat 6.99%) should produce different results than Code D (table-driven)
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var valuesD = new StateInputValues { ["WithholdingCode"] = "Code D" };
        var valuesNoForm = new StateInputValues { ["WithholdingCode"] = "No Form CT-W4" };

        var resultD = calc.Calculate(context, valuesD);
        var resultNoForm = calc.Calculate(context, valuesNoForm);

        Assert.NotEqual(resultD.Withholding, resultNoForm.Withholding);
        // No Form = 5000 * 0.0699 = 349.50
        Assert.Equal(349.50m, resultNoForm.Withholding);
    }

    // ── Additional and reduced withholding ──────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingCode"] = "Code A",
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // Base = 145.77 + 25 = 170.77
        Assert.Equal(170.77m, result.Withholding);
    }

    [Fact]
    public void ReducedWithholding_IsSubtracted()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingCode"] = "Code A",
            ["ReducedWithholding"] = 30m
        };

        var result = calc.Calculate(context, values);

        // Base = 145.77 - 30 = 115.77
        Assert.Equal(115.77m, result.Withholding);
    }

    [Fact]
    public void ReducedWithholding_FloorsAtZero()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingCode"] = "Code A",
            ["ReducedWithholding"] = 999m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        // taxable wages = 3000 - 500 = 2500
        // S = 2500 * 26 = 65,000
        // Table A: S > 35,000 → exemption = 0
        // TI = 65,000
        // Table B: 2000 + 0.055 * (65000 - 50000) = 2000 + 825 = 2825
        // Table C: S > 62,750 and <= 65,250 → add_back = 150
        // Table D: S <= 105,000 → recapture = 0
        // Pre-credit = 2825 + 150 = 2975
        // Table E: S > 52,500 → credit = 0.00
        // Annual = 2975
        // Per-period = 2975 / 26 = 114.42
        Assert.Equal(2500m, result.TaxableWages);
        Assert.Equal(114.42m, result.Withholding);
    }

    // ── Zero wages ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Taxable income <= 0 after exemption ─────────────────────────

    [Fact]
    public void ExemptionExceedsSalary_ZeroWithholding()
    {
        // Code A, annual, $10,000
        // S = 10,000
        // Table A: exemption = 12,000 (S <= 24,000)
        // TI = max(10000 - 12000, 0) = 0
        // => basePerPeriodWithholding = 0
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 10000m,
            PayPeriod: PayFrequency.Annual, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        Assert.Equal(10000m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── CT PFMLI (Paid Family and Medical Leave Insurance) ─────────

    [Fact]
    public void Pfmli_SimpleGrossWages()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        // PFMLI = 5000 × 0.005 = 25.00
        Assert.Equal(25.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfmli_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 10000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);
        var values = new StateInputValues { ["WithholdingCode"] = "Code A" };

        var result = calc.Calculate(context, values);

        // PFMLI uses gross wages (10000), not reduced wages (8000)
        // PFMLI = 10000 × 0.005 = 50.00
        Assert.Equal(50.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfmli_CodeE_StillApplied()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "Code E" };

        var result = calc.Calculate(context, values);

        // PFMLI = 4000 × 0.005 = 20.00 (applies even with Code E)
        Assert.Equal(20.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfmli_NoFormCtW4_StillApplied()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 6000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = "No Form CT-W4" };

        var result = calc.Calculate(context, values);

        // PFMLI = 6000 × 0.005 = 30.00 (applies regardless of withholding code)
        Assert.Equal(30.00m, result.DisabilityInsurance);
    }

    [Theory]
    [InlineData("Code A")]
    [InlineData("Code E")]
    [InlineData("No Form CT-W4")]
    public void DisabilityInsuranceLabel_IsPaidFamilyAndMedicalLeave(string code)
    {
        // Connecticut PFMLI should be labeled "Family Leave Insurance",
        // not the generic "State Disability Insurance" used by California.
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = code };

        var result = calc.Calculate(context, values);

        // CT's program is Paid Family and Medical Leave; "FLI" is New Jersey's
        // separate program and would be misleading now that NJ withholds one.
        Assert.Equal("Paid Family and Medical Leave (PFMLI)", result.DisabilityInsuranceLabel);
    }

    // ── Default inputs ──────────────────────────────────────────────

    [Fact]
    public void DefaultInputs_UsesCodeA()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);

        // No explicit WithholdingCode → defaults to "Code A"
        var result = calc.Calculate(context, new StateInputValues());

        // Same as CodeA_Biweekly_3000 = 145.77
        Assert.Equal(145.77m, result.Withholding);
    }

    // ── Regression: all codes A–F must produce non-zero withholding for typical wages ──

    [Theory]
    [InlineData("Code A")]
    [InlineData("Code B")]
    [InlineData("Code C")]
    [InlineData("Code D")]
    [InlineData("Code F")]
    public void AllTableDrivenCodes_Biweekly3000_ProduceNonZeroWithholding(string code)
    {
        // Regression: codes A–F must not return $0.00 for typical wages.
        // Each code uses the table-driven path with 5 lookup tables.
        // S = 3000 * 26 = 78,000 — well above exemption thresholds for every code.
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues { ["WithholdingCode"] = code };

        var result = calc.Calculate(context, values);

        Assert.True(result.Withholding > 0m,
            $"{code}: expected non-zero withholding for $3,000 biweekly but got {result.Withholding}");
    }

    // ── Regression: null / empty WithholdingCode must fall back to Code A ──

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NullOrEmptyWithholdingCode_FallsBackToCodeA(string? code)
    {
        // The calculator must handle null/empty WithholdingCode by falling
        // back to Code A rather than returning $0.00.
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.CT, GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);

        var values = new StateInputValues { ["WithholdingCode"] = code };
        var result = calc.Calculate(context, values);

        // Same as CodeA_Biweekly_3000 = 145.77
        Assert.Equal(145.77m, result.Withholding);
    }

    [Fact]
    public void Validate_NullWithholdingCode_PassesWithDefaultFallback()
    {
        // When WithholdingCode is null or missing, Validate should not produce
        // an error because the calculator falls back to Code A.
        var calc = LoadCalculator();

        var valuesNull = new StateInputValues { ["WithholdingCode"] = (string?)null };
        Assert.Empty(calc.Validate(valuesNull));

        var valuesEmpty = new StateInputValues();
        Assert.Empty(calc.Validate(valuesEmpty));
    }
}
