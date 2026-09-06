using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Massachusetts;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Tests;
using Xunit;

public class MassachusettsWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMassachusetts()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        Assert.Equal(UsState.MA, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("M-4 Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    [Fact]
    public void Schema_ContainsDependents()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Dependents");
        Assert.Equal("Dependents", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsBlindExemptions()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "BlindExemptions");
        Assert.Equal("Blind Exemptions", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAgeExemptions()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AgeExemptions");
        Assert.Equal("Age 65+ Exemptions", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSingleStatus_ReturnsEmpty()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidMarriedStatus_ReturnsEmpty()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Married" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidHeadOfHouseholdStatus_ReturnsEmpty()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Head of Household" });
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues { ["FilingStatus"] = "Invalid" });
        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeDependents_ReturnsError()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Dependents", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeBlindExemptions_ReturnsError()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["BlindExemptions"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Blind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeAgeExemptions_ReturnsError()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var errors = calc.Validate(new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AgeExemptions"] = -1
        });
        Assert.Contains(errors, e => e.Contains("Age", StringComparison.OrdinalIgnoreCase));
    }

    // ── Flat 5% — Single, no exemptions ─────────────────────────────

    [Fact]
    public void Biweekly_Single_NoExemptions_FlatRateOnAnnualizedWages()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 0,
            ["BlindExemptions"] = 0,
            ["AgeExemptions"] = 0
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 3000 * 26 = 78,000
        // Personal exemption (Single) = 4,400
        // Annual taxable = 78,000 - 4,400 = 73,600
        // Tax = 73,600 * 5% = 3,680
        // Per period = 3,680 / 26 = 141.538461..., rounds to 141.54
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(141.54m, result.Withholding);
    }

    [Fact]
    public void Monthly_Married_NoExemptions_PersonalExemptionApplied()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married"
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 6000 * 12 = 72,000
        // Personal exemption (Married) = 8,800
        // Annual taxable = 72,000 - 8,800 = 63,200
        // Tax = 63,200 * 5% = 3,160
        // Per period = 3,160 / 12 = 263.3333..., rounds to 263.33
        Assert.Equal(6000m, result.TaxableWages);
        Assert.Equal(263.33m, result.Withholding);
    }

    [Fact]
    public void Biweekly_HeadOfHousehold_NoExemptions_HoHPersonalExemptionApplied()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household"
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 3000 * 26 = 78,000
        // Personal exemption (Head of Household) = 6,800
        // Annual taxable = 78,000 - 6,800 = 71,200
        // Tax = 71,200 * 5% = 3,560
        // Per period = 3,560 / 26 = 136.923076..., rounds to 136.92
        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(136.92m, result.Withholding);
    }

    // ── Dependent exemption ($1,000 each) ───────────────────────────

    [Fact]
    public void Monthly_Single_TwoDependents_ReduceTaxableIncome()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Dependents"] = 2
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 5000 * 12 = 60,000
        // Personal exemption (Single) = 4,400
        // Dependent deductions = 2 * 1,000 = 2,000
        // Annual taxable = 60,000 - 4,400 - 2,000 = 53,600
        // Tax = 53,600 * 5% = 2,680
        // Per period = 2,680 / 12 = 223.3333..., rounds to 223.33
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(223.33m, result.Withholding);
    }

    // ── Blind exemption ($2,200 each) ────────────────────────────────

    [Fact]
    public void Weekly_Single_OneBlindExemption_ReducesTaxableIncome()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["BlindExemptions"] = 1
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 1000 * 52 = 52,000
        // Personal exemption (Single) = 4,400
        // Blind exemption = 1 * 2,200 = 2,200
        // Annual taxable = 52,000 - 4,400 - 2,200 = 45,400
        // Tax = 45,400 * 5% = 2,270
        // Per period = 2,270 / 52 = 43.653846..., rounds to 43.65
        Assert.Equal(1000m, result.TaxableWages);
        Assert.Equal(43.65m, result.Withholding);
    }

    // ── Age 65+ exemption ($700 each) ───────────────────────────────

    [Fact]
    public void Monthly_Married_TwoAgeExemptions_ReduceTaxableIncome()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["AgeExemptions"] = 2
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 4000 * 12 = 48,000
        // Personal exemption (Married) = 8,800
        // Age 65+ exemptions = 2 * 700 = 1,400
        // Annual taxable = 48,000 - 8,800 - 1,400 = 37,800
        // Tax = 37,800 * 5% = 1,890
        // Per period = 1,890 / 12 = 157.50
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(157.50m, result.Withholding);
    }

    // ── 4% surtax on income over $1,000,000 ─────────────────────────

    [Fact]
    public void Annual_Single_IncomeAbove1M_SurtaxApplied()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        // $1,200,000 annual wages, no exemptions (single, filing with $4,400 exemption)
        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 1_200_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single"
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 1,200,000
        // Personal exemption (Single) = 4,400
        // Annual taxable = 1,200,000 - 4,400 = 1,195,600
        // Tax:
        //   Up to $1,000,000: 1,000,000 * 5% = 50,000
        //   Over $1,000,000:  195,600 * 9% = 17,604
        //   Total = 67,604
        // Per period (annual = 1) = 67,604 / 1 = 67,604.00
        Assert.Equal(1_200_000m, result.TaxableWages);
        Assert.Equal(67_604.00m, result.Withholding);
    }

    [Fact]
    public void Biweekly_Single_HighIncome_SurtaxApplied()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        // Biweekly $50,000 gross (annualizes to $1,300,000), no exemptions
        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 50_000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single"
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 50,000 * 26 = 1,300,000
        // Personal exemption (Single) = 4,400
        // Annual taxable = 1,300,000 - 4,400 = 1,295,600
        // Tax:
        //   Up to $1,000,000: 1,000,000 * 5% = 50,000
        //   Over $1,000,000:  295,600 * 9% = 26,604
        //   Total annual tax = 76,604
        // Per period = 76,604 / 26 = 2,946.307692..., rounds to 2,946.31
        Assert.Equal(50_000m, result.TaxableWages);
        Assert.Equal(2_946.31m, result.Withholding);
    }

    [Fact]
    public void Annual_Single_IncomeExactly1M_NoSurtax()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        // Exactly $1,000,000 annual wages
        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 1_000_000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single"
        };

        var result = calc.Calculate(context, values);

        // Annual wages = 1,000,000
        // Personal exemption (Single) = 4,400
        // Annual taxable = 1,000,000 - 4,400 = 995,600
        // Tax = 995,600 * 5% = 49,780 (entirely below surtax threshold)
        Assert.Equal(1_000_000m, result.TaxableWages);
        Assert.Equal(49_780.00m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceStateTaxableWages()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single"
        };

        var result = calc.Calculate(context, values);

        // Taxable wages = 4000 - 500 = 3500
        // Annual wages = 3500 * 26 = 91,000
        // Personal exemption (Single) = 4,400
        // Annual taxable = 91,000 - 4,400 = 86,600
        // Tax = 86,600 * 5% = 4,330
        // Per period = 4,330 / 26 = 166.538461..., rounds to 166.54
        Assert.Equal(3500m, result.TaxableWages);
        Assert.Equal(166.54m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAddedAfterTaxCalc()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // Base = 141.54 (from single biweekly $3,000 test above) + 25 = 166.54
        Assert.Equal(166.54m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void LowIncome_PersonalExemptionExceedsAnnualWages_ZeroTax()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        // Biweekly $100: annual = $2,600, personal exemption = $4,400 > wages
        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 100m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["FilingStatus"] = "Single" };

        var result = calc.Calculate(context, values);

        Assert.Equal(100m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_AllExemptionTypes_PreTaxAndExtraWithholding()
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        // Married employee, $5,000 gross biweekly, $300 pre-tax,
        // 2 dependents, 1 blind exemption, 1 age-65+ exemption, $20 extra.
        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 300m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Dependents"] = 2,
            ["BlindExemptions"] = 1,
            ["AgeExemptions"] = 1,
            ["AdditionalWithholding"] = 20m
        };

        var result = calc.Calculate(context, values);

        // Taxable wages = 5000 - 300 = 4700
        // Annual wages = 4700 * 26 = 122,200
        // Personal exemption (Married) = 8,800
        // Dependent deductions = 2 * 1,000 = 2,000
        // Blind exemption = 1 * 2,200 = 2,200
        // Age 65+ exemption = 1 * 700 = 700
        // Total exemption = 8,800 + 2,000 + 2,200 + 700 = 13,700
        // Annual taxable = 122,200 - 13,700 = 108,500
        // Tax = 108,500 * 5% = 5,425
        // Per period = 5,425 / 26 = 208.653846..., rounds to 208.65
        // Total = 208.65 + 20 = 228.65
        Assert.Equal(4700m, result.TaxableWages);
        Assert.Equal(228.65m, result.Withholding);
    }

    // ── PFML employee contributions ───────────────────────────────────
    //
    // Massachusetts DFML 2026: employees contribute 0.28% of eligible wages for
    // medical leave and 0.18% for family leave (0.46% combined), capped by the
    // Social Security taxable maximum of $184,500.
    // https://www.mass.gov/info-details/paid-family-and-medical-leave-employer-contribution-rates-and-calculator

    [Fact]
    public void Pfml_MedicalAndFamilyLeave_AreSeparateLines()
    {
        // Medical: 3,000 × 0.28% = 8.40
        // Family:  3,000 × 0.18% = 5.40
        var result = CalculateMa(grossWages: 3000m);

        var medical = Assert.Single(result.TaxLines!, l => l.ShortCode == "PFML-M");
        var family = Assert.Single(result.TaxLines!, l => l.ShortCode == "PFML-F");

        Assert.Equal(8.40m, medical.Amount);
        Assert.Equal(5.40m, family.Amount);
        Assert.Equal(13.80m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfml_CollapsedLabel_NamesBothPrograms()
    {
        var result = CalculateMa(grossWages: 3000m);

        Assert.Equal("State Disability & Paid Leave", result.DisabilityInsuranceLabel);
    }

    [Fact]
    public void Pfml_OrderedByAmount_MedicalBeforeFamily()
    {
        var result = PayCalculatorTestHarness.StateLines(UsState.MA, grossWages: 3000m);

        var assessments = result
            .Where(l => l.Kind == StateTaxLineKind.PayrollAssessment)
            .ToList();

        Assert.Equal("PFML-M", assessments[0].ShortCode);
        Assert.Equal("PFML-F", assessments[1].ShortCode);
    }

    [Fact]
    public void Pfml_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        // The premium follows gross wages, so the $1,000 pre-tax deduction
        // must not shrink it: 3,000 × 0.46% = 13.80.
        var result = CalculateMa(grossWages: 3000m, preTaxDeductions: 1000m);

        Assert.Equal(13.80m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfml_StopsAtSocialSecurityWageBase()
    {
        // $180,000 already earned leaves $4,500 of the $184,500 base.
        // Medical: 4,500 × 0.28% = 12.60; family: 4,500 × 0.18% = 8.10.
        var result = CalculateMa(grossWages: 10_000m, ytdStateWages: 180_000m);

        Assert.Equal(20.70m, result.DisabilityInsurance);
    }

    [Fact]
    public void Pfml_AtWageBase_WithholdsNothingFurther()
    {
        var result = CalculateMa(grossWages: 10_000m, ytdStateWages: 184_500m);

        Assert.Equal(0m, result.DisabilityInsurance);
        Assert.DoesNotContain(result.TaxLines!, l => l.Kind == StateTaxLineKind.PayrollAssessment);
    }

    [Fact]
    public void Pfml_RoundsAwayFromZero()
    {
        // 125 × 0.18% = 0.225 exactly — a true midpoint, so away-from-zero gives
        // 0.23 where banker's rounding would give 0.22.
        var result = CalculateMa(grossWages: 125m);

        var family = Assert.Single(result.TaxLines!, l => l.ShortCode == "PFML-F");
        Assert.Equal(0.23m, family.Amount);
    }

    private static StateWithholdingResult CalculateMa(
        decimal grossWages,
        decimal preTaxDeductions = 0m,
        decimal ytdStateWages = 0m)
    {
        var calc = new MassachusettsWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.MA,
            GrossWages: grossWages,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions,
            YtdStateWages: ytdStateWages);

        return calc.Calculate(context, new StateInputValues());
    }
}
