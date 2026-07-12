using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.NewMexico;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for New Mexico (NM) state income tax withholding.
/// New Mexico uses the dedicated <see cref="NewMexicoWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the NM annualized
/// percentage-method formula (2026) per FYI-104 and NMSA §7-2-7:
///   taxable wages  = gross wages − pre-tax deductions (floor $0)
///   annual wages   = taxable wages × pay periods
///   annual taxable = max(0, annual wages − std ded − (exemptions × $4,000))
///   annual tax     = brackets applied to annual taxable income
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 New Mexico parameters:
///   Standard deduction: $15,750 (Single) / $31,500 (Married) / $23,625 (HoH)
///   Per-exemption deduction (RPD-41272): $4,000
///   Single brackets:
///     1.7% on $0 – $5,500
///     3.2% on $5,500 – $11,000
///     4.7% on $11,000 – $16,000
///     4.9% on $16,000 – $210,000
///     5.9% over $210,000
///   Married / Head of Household brackets (identical thresholds):
///     1.7% on $0 – $8,000
///     3.2% on $8,000 – $16,000
///     4.7% on $16,000 – $24,000
///     4.9% on $24,000 – $315,000
///     5.9% over $315,000
/// </summary>
public class NewMexicoWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNewMexico()
    {
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.NM, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Exemptions_AdditionalWithholding()
    {
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single — first bracket only ─────────────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracketOnly()
    {
        // annual = $700 × 26 = $18,200
        // std ded = $15,750
        // annual taxable = $18,200 − $15,750 = $2,450  (in first bracket $0–$5,500)
        // annual tax = $2,450 × 0.017 = $41.65
        // per period = $41.65 / 26 = $1.601923... → $1.60
        var result = Calculate(GrossWages: 700m, PayFrequency.Biweekly, "Single");

        Assert.Equal(700m, result.TaxableWages);
        Assert.Equal(1.60m, result.Withholding);
    }

    // ── Single — spans first and second brackets ─────────────────────

    [Fact]
    public void Single_Biweekly_SpansFirstAndSecondBrackets()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $15,750
        // annual taxable = $26,000 − $15,750 = $10,250
        // annual tax = $5,500 × 0.017 + ($10,250 − $5,500) × 0.032
        //            = $93.50 + $4,750 × 0.032
        //            = $93.50 + $152.00 = $245.50
        // per period = $245.50 / 26 = $9.442307... → $9.44
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(9.44m, result.Withholding);
    }

    // ── Single — spans all five brackets ────────────────────────────

    [Fact]
    public void Single_Monthly_TopBracket()
    {
        // annual = $20,000 × 12 = $240,000
        // std ded = $15,750
        // annual taxable = $240,000 − $15,750 = $224,250
        // annual tax = $5,500 × 0.017
        //            + ($11,000 − $5,500) × 0.032
        //            + ($16,000 − $11,000) × 0.047
        //            + ($210,000 − $16,000) × 0.049
        //            + ($224,250 − $210,000) × 0.059
        //            = $93.50 + $176.00 + $235.00 + $9,506.00 + $840.75
        //            = $10,851.25
        // per period = $10,851.25 / 12 = $904.270833... → $904.27
        var result = Calculate(GrossWages: 20_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(20_000m, result.TaxableWages);
        Assert.Equal(904.27m, result.Withholding);
    }

    // ── Single — below standard deduction (zero withholding) ────────

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,000 × 12 = $12,000; std ded = $15,750
        // annual taxable = max(0, $12,000 − $15,750) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married — spans first two brackets ──────────────────────────

    [Fact]
    public void Married_Biweekly_SpansFirstAndSecondBrackets()
    {
        // annual = $2,000 × 26 = $52,000
        // std ded = $31,500
        // annual taxable = $52,000 − $31,500 = $20,500
        // annual tax = $8,000 × 0.017
        //            + ($16,000 − $8,000) × 0.032
        //            + ($20,500 − $16,000) × 0.047
        //            = $136.00 + $256.00 + $211.50
        //            = $603.50
        // per period = $603.50 / 26 = $23.211538... → $23.21
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(23.21m, result.Withholding);
    }

    // ── Married — top bracket ────────────────────────────────────────

    [Fact]
    public void Married_Monthly_TopBracket()
    {
        // annual = $30,000 × 12 = $360,000
        // std ded = $31,500
        // annual taxable = $360,000 − $31,500 = $328,500
        // annual tax = $8,000 × 0.017
        //            + ($16,000 − $8,000) × 0.032
        //            + ($24,000 − $16,000) × 0.047
        //            + ($315,000 − $24,000) × 0.049
        //            + ($328,500 − $315,000) × 0.059
        //            = $136.00 + $256.00 + $376.00 + $14,259.00 + $796.50
        //            = $15,823.50
        // per period = $15,823.50 / 12 = $1,318.625 → $1,318.63
        var result = Calculate(GrossWages: 30_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(30_000m, result.TaxableWages);
        Assert.Equal(1_318.63m, result.Withholding);
    }

    // ── Married — below standard deduction ──────────────────────────

    [Fact]
    public void Married_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $2,000 × 12 = $24,000; std ded = $31,500
        // annual taxable = max(0, $24,000 − $31,500) = $0
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household — uses Married bracket thresholds ─────────

    [Fact]
    public void HeadOfHousehold_Biweekly_SpansFirstAndSecondBrackets()
    {
        // HoH uses Married bracket thresholds ($8,000/$16,000/$24,000/$315,000)
        // annual = $2,500 × 26 = $65,000
        // std ded = $23,625 (HoH)
        // annual taxable = $65,000 − $23,625 = $41,375
        // annual tax = $8,000 × 0.017
        //            + ($16,000 − $8,000) × 0.032
        //            + ($24,000 − $16,000) × 0.047
        //            + ($41,375 − $24,000) × 0.049
        //            = $136.00 + $256.00 + $376.00 + $851.375
        //            = $1,619.375
        // per period = $1,619.375 / 26 = $62.283653... → $62.28
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(62.28m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_TopBracket()
    {
        // annual = $30,000 × 12 = $360,000
        // std ded = $23,625
        // annual taxable = $360,000 − $23,625 = $336,375
        // annual tax = $8,000 × 0.017
        //            + ($16,000 − $8,000) × 0.032
        //            + ($24,000 − $16,000) × 0.047
        //            + ($315,000 − $24,000) × 0.049
        //            + ($336,375 − $315,000) × 0.059
        //            = $136.00 + $256.00 + $376.00 + $14,259.00 + $1,261.125
        //            = $16,288.125
        // per period = $16,288.125 / 12 = $1,357.34375 → $1,357.34
        var result = Calculate(GrossWages: 30_000m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(30_000m, result.TaxableWages);
        Assert.Equal(1_357.34m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,500 × 12 = $18,000; HoH std ded = $23,625
        // annual taxable = max(0, $18,000 − $23,625) = $0
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── RPD-41272 Exemptions ─────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneExemption_ReducesTax()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $15,750; 1 exemption = $4,000
        // annual taxable = $26,000 − $15,750 − $4,000 = $6,250
        // annual tax = $5,500 × 0.017 + ($6,250 − $5,500) × 0.032
        //            = $93.50 + $750 × 0.032
        //            = $93.50 + $24.00 = $117.50
        // per period = $117.50 / 26 = $4.519230... → $4.52
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 1);

        Assert.Equal(4.52m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoExemptions_ReduceTaxFurther()
    {
        // annual = $1,000 × 26 = $26,000
        // std ded = $15,750; 2 exemptions = $8,000
        // annual taxable = $26,000 − $15,750 − $8,000 = $2,250  (in first bracket)
        // annual tax = $2,250 × 0.017 = $38.25
        // per period = $38.25 / 26 = $1.471153... → $1.47
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 2);

        Assert.Equal(1.47m, result.Withholding);
    }

    [Fact]
    public void Exemptions_EliminateAllTax_ReturnsZero()
    {
        // annual = $1,000 × 26 = $26,000; std ded = $15,750
        // 5 exemptions = $20,000 → taxable = max(0, $26,000 − $15,750 − $20,000) = $0
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", exemptions: 5);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TwoExemptions()
    {
        // annual = $2,000 × 26 = $52,000
        // std ded = $31,500; 2 exemptions = $8,000
        // annual taxable = $52,000 − $31,500 − $8,000 = $12,500
        // annual tax = $8,000 × 0.017 + ($12,500 − $8,000) × 0.032
        //            = $136.00 + $4,500 × 0.032
        //            = $136.00 + $144.00 = $280.00
        // per period = $280.00 / 26 = $10.769230... → $10.77
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Married", exemptions: 2);

        Assert.Equal(10.77m, result.Withholding);
    }

    // ── Bracket boundary — Single at exactly $5,500 ──────────────────

    [Fact]
    public void Single_Annual_ExactlyAtFirstBracketCeiling()
    {
        // annual = $21,250 (annual pay)
        // std ded = $15,750
        // annual taxable = $21,250 − $15,750 = $5,500  (hits first bracket ceiling)
        // annual tax = $5,500 × 0.017 = $93.50
        // per period = $93.50 / 1 = $93.50
        var result = Calculate(GrossWages: 21_250m, PayFrequency.Annual, "Single");

        Assert.Equal(93.50m, result.Withholding);
    }

    // ── Bracket boundary — Married at exactly $8,000 ─────────────────

    [Fact]
    public void Married_Annual_ExactlyAtFirstBracketCeiling()
    {
        // annual = $39,500
        // std ded = $31,500
        // annual taxable = $39,500 − $31,500 = $8,000  (hits first bracket ceiling)
        // annual tax = $8,000 × 0.017 = $136.00
        // per period = $136.00 / 1 = $136.00
        var result = Calculate(GrossWages: 39_500m, PayFrequency.Annual, "Married");

        Assert.Equal(136.00m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from Single_Biweekly_SpansFirstAndSecondBrackets = $9.44; extra = $15.00 → $24.44
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            additionalWithholding: 15m);

        Assert.Equal(24.44m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $1,000, pre-tax $200 → taxable wages = $800
        // annual = $800 × 26 = $20,800
        // std ded = $15,750
        // annual taxable = $20,800 − $15,750 = $5,050  (in first bracket)
        // annual tax = $5,050 × 0.017 = $85.85
        // per period = $85.85 / 26 = $3.301923... → $3.30
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 200m);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(3.30m, result.Withholding);
    }

    // ── Semimonthly pay frequency ────────────────────────────────────

    [Fact]
    public void Single_Semimonthly_CorrectDeannualization()
    {
        // annual = $1,200 × 24 = $28,800
        // std ded = $15,750
        // annual taxable = $28,800 − $15,750 = $13,050
        // annual tax = $5,500 × 0.017 + ($11,000 − $5,500) × 0.032
        //              + ($13,050 − $11,000) × 0.047
        //            = $93.50 + $176.00 + $2,050 × 0.047
        //            = $93.50 + $176.00 + $96.35
        //            = $365.85
        // per period = $365.85 / 24 = $15.24375 → $15.24
        var result = Calculate(GrossWages: 1_200m, PayFrequency.Semimonthly, "Single");

        Assert.Equal(15.24m, result.Withholding);
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
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NewMexicoWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.NM,
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
