using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Montana;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for Montana (MT) state income tax withholding.
/// Montana uses the dedicated <see cref="MontanaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Montana DOR annualized
/// percentage-method formula (2026):
///   std ded        = max(stdMin, min(annualWages × 20%, stdMax))
///   annual taxable = max(0, annualWages − std ded − (exemptions × $3,040))
///   annual tax     = min(taxable, $23,800) × 4.7%
///                  + max(0, taxable − $23,800) × 5.9%
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Montana parameters:
///   Standard deduction: 20% of annual wages
///     Single / MFS:  minimum $4,370 / maximum $5,310
///     Married / HoH: minimum $8,740 / maximum $10,620
///   Per-exemption deduction (MW-4): $3,040
///   Brackets (all filing statuses):
///     4.7% on $0 – $23,800
///     5.9% over $23,800
/// </summary>
public class MontanaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMontana()
    {
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.MT, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Exemptions_AdditionalWithholding()
    {
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer — standard deduction between min and max ────────

    [Fact]
    public void Single_Biweekly_StandardDeductionBetweenMinAndMax()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = max($4,370, min($26,000 × 20%, $5,310))
        //         = max($4,370, min($5,200, $5,310)) = $5,200 (between limits)
        // annual taxable = $26,000 − $5,200 = $20,800
        // annual tax = $20,800 × 0.047 = $977.60 (entirely in lower bracket)
        // per period = $977.60 / 26 = $37.60
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(37.60m, result.Withholding);
    }

    // ── Single filer — standard deduction at minimum (low wages) ────

    [Fact]
    public void Single_Monthly_StandardDeductionAtMinimum()
    {
        // annual = $1,500 × 12 = $18,000
        // std ded = max($4,370, min($18,000 × 20%, $5,310))
        //         = max($4,370, min($3,600, $5,310)) = $4,370 (minimum applies)
        // annual taxable = $18,000 − $4,370 = $13,630
        // annual tax = $13,630 × 0.047 = $640.61
        // per period = $640.61 / 12 = $53.384166... → $53.38
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(53.38m, result.Withholding);
    }

    // ── Single filer — standard deduction at maximum (high wages) ───

    [Fact]
    public void Single_Biweekly_StandardDeductionAtMaximum_TopBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = max($4,370, min($78,000 × 20%, $5,310))
        //         = max($4,370, min($15,600, $5,310)) = $5,310 (maximum applies)
        // annual taxable = $78,000 − $5,310 = $72,690
        // annual tax = $23,800 × 0.047 + ($72,690 − $23,800) × 0.059
        //            = $1,118.60 + $48,890 × 0.059
        //            = $1,118.60 + $2,884.51 = $4,003.11
        // per period = $4,003.11 / 26 = $153.965769... → $153.97
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(153.97m, result.Withholding);
    }

    // ── Single filer — annual taxable income below zero (zero withholding)

    [Fact]
    public void Single_Monthly_BelowStandardDeductionMinimum_ReturnsZero()
    {
        // annual = $300 × 12 = $3,600
        // std ded = max($4,370, min($720, $5,310)) = $4,370
        // annual taxable = max(0, $3,600 − $4,370) = $0
        var result = Calculate(GrossWages: 300m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married filer — standard deduction at minimum ────────────────

    [Fact]
    public void Married_Monthly_StandardDeductionAtMinimum()
    {
        // annual = $3,000 × 12 = $36,000
        // std ded = max($8,740, min($36,000 × 20%, $10,620))
        //         = max($8,740, min($7,200, $10,620)) = $8,740 (minimum applies)
        // annual taxable = $36,000 − $8,740 = $27,260
        // annual tax = $23,800 × 0.047 + ($27,260 − $23,800) × 0.059
        //            = $1,118.60 + $3,460 × 0.059
        //            = $1,118.60 + $204.14 = $1,322.74
        // per period = $1,322.74 / 12 = $110.228333... → $110.23
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(110.23m, result.Withholding);
    }

    // ── Married filer — standard deduction at maximum ────────────────

    [Fact]
    public void Married_Biweekly_StandardDeductionAtMaximum_TopBracket()
    {
        // annual = $4,000 × 26 = $104,000
        // std ded = max($8,740, min($104,000 × 20%, $10,620))
        //         = max($8,740, min($20,800, $10,620)) = $10,620 (maximum applies)
        // annual taxable = $104,000 − $10,620 = $93,380
        // annual tax = $23,800 × 0.047 + ($93,380 − $23,800) × 0.059
        //            = $1,118.60 + $69,580 × 0.059
        //            = $1,118.60 + $4,105.22 = $5,223.82
        // per period = $5,223.82 / 26 = $200.916923... → $200.92
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(200.92m, result.Withholding);
    }

    // ── Married filer — below standard deduction minimum ─────────────

    [Fact]
    public void Married_Biweekly_BelowStandardDeductionMinimum_ReturnsZero()
    {
        // annual = $300 × 26 = $7,800
        // std ded = max($8,740, min($1,560, $10,620)) = $8,740
        // annual taxable = max(0, $7,800 − $8,740) = $0
        var result = Calculate(GrossWages: 300m, PayFrequency.Biweekly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household — uses married standard-deduction limits ───

    [Fact]
    public void HeadOfHousehold_Monthly_StandardDeductionAtMinimum()
    {
        // HoH uses married standard-deduction limits per MT DOR instructions.
        // annual = $3,500 × 12 = $42,000
        // std ded = max($8,740, min($42,000 × 20%, $10,620))
        //         = max($8,740, min($8,400, $10,620)) = $8,740 (minimum applies)
        // annual taxable = $42,000 − $8,740 = $33,260
        // annual tax = $23,800 × 0.047 + ($33,260 − $23,800) × 0.059
        //            = $1,118.60 + $9,460 × 0.059
        //            = $1,118.60 + $558.14 = $1,676.74
        // per period = $1,676.74 / 12 = $139.728333... → $139.73
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(3_500m, result.TaxableWages);
        Assert.Equal(139.73m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_StandardDeductionAtMaximum()
    {
        // HoH uses married limits; annual = $4,000 × 26 = $104,000
        // std ded = $10,620 (maximum applies, same calc as Married above)
        // annual taxable = $104,000 − $10,620 = $93,380
        // annual tax = $5,223.82; per period = $200.92
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(200.92m, result.Withholding);
    }

    // ── Single filer — Monthly, standard deduction near maximum ─────

    [Fact]
    public void Single_Monthly_LowerBracketOnly()
    {
        // annual = $2,000 × 12 = $24,000
        // std ded = max($4,370, min($24,000 × 20%, $5,310))
        //         = max($4,370, min($4,800, $5,310)) = $4,800 (between limits)
        // annual taxable = $24,000 − $4,800 = $19,200  (entirely in lower bracket)
        // annual tax = $19,200 × 0.047 = $902.40
        // per period = $902.40 / 12 = $75.20
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(75.20m, result.Withholding);
    }

    // ── MW-4 Exemptions ──────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneExemption_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = $5,310 (maximum)
        // 1 exemption = $3,040
        // annual taxable = $78,000 − $5,310 − $3,040 = $69,650
        // annual tax = $23,800 × 0.047 + ($69,650 − $23,800) × 0.059
        //            = $1,118.60 + $45,850 × 0.059
        //            = $1,118.60 + $2,705.15 = $3,823.75
        // per period = $3,823.75 / 26 = $147.067307... → $147.07
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", exemptions: 1);

        Assert.Equal(147.07m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoExemptions_ReduceTaxFurther()
    {
        // annual = $3,000 × 26 = $78,000
        // std ded = $5,310 (maximum)
        // 2 exemptions = $6,080
        // annual taxable = $78,000 − $5,310 − $6,080 = $66,610
        // annual tax = $23,800 × 0.047 + ($66,610 − $23,800) × 0.059
        //            = $1,118.60 + $42,810 × 0.059
        //            = $1,118.60 + $2,525.79 = $3,644.39
        // per period = $3,644.39 / 26 = $140.168076... → $140.17
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", exemptions: 2);

        Assert.Equal(140.17m, result.Withholding);
    }

    [Fact]
    public void Exemptions_EliminateAllTax_ReturnsZero()
    {
        // annual = $1,000 × 26 = $26,000; std ded = $5,200
        // 10 exemptions = $30,400 → taxable = max(0, $26,000 − $5,200 − $30,400) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 10);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Monthly_LowerBracketOnly = $75.20; extra = $15.00 → $90.20
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 15m);

        Assert.Equal(90.20m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // std ded = max($4,370, min($65,000 × 20%, $5,310))
        //         = max($4,370, min($13,000, $5,310)) = $5,310 (maximum)
        // annual taxable = $65,000 − $5,310 = $59,690
        // annual tax = $23,800 × 0.047 + ($59,690 − $23,800) × 0.059
        //            = $1,118.60 + $35,890 × 0.059
        //            = $1,118.60 + $2,117.51 = $3,236.11
        // per period = $3,236.11 / 26 = $124.465769... → $124.47
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(124.47m, result.Withholding);
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
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
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
        var calc = new MontanaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.MT,
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
