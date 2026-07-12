using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Missouri;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Missouri (MO) state income tax withholding.
/// Missouri uses the dedicated <see cref="MissouriWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Missouri Department of
/// Revenue annualized percentage-method formula (2026):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − (allowances × $2,100))
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Missouri parameters:
///   Standard deduction: $15,750 (Single) / $31,500 (Married) / $23,625 (Head of Household)
///   Per-allowance deduction: $2,100 (MO W-4)
///   Brackets (all filing statuses):
///     0%   on $0 – $1,313
///     2%   on $1,313 – $2,626
///     2.5% on $2,626 – $3,939
///     3%   on $3,939 – $5,252
///     3.5% on $5,252 – $6,565
///     4%   on $6,565 – $7,878
///     4.5% on $7,878 – $9,191
///     4.7% over $9,191
/// </summary>
public class MissouriWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsMissouri()
    {
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.MO, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal("Single", field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains("Single", field.Options);
        Assert.Contains("Married", field.Options);
        Assert.Contains("Head of Household", field.Options);
    }

    // ── Single filer — straddles zero-rate and first taxable bracket ─

    [Fact]
    public void Single_Monthly_InZeroAndFirstBracket()
    {
        // annual = $1,500 × 12 = $18,000
        // less std ded (single) = $15,750 → taxable = $2,250
        // 0%  on $0–$1,313          = $0
        // 2%  on $1,313–$2,250      = $937 × 0.02 = $18.74
        // per period = $18.74 / 12 = $1.561666... → $1.56
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(1.56m, result.Withholding);
    }

    // ── Single filer — annual taxable inside zero-rate bracket ───────

    [Fact]
    public void Single_Monthly_AnnualTaxableInZeroRateBracket_ReturnsZero()
    {
        // annual = $1,400 × 12 = $16,800
        // less std ded (single) = $15,750 → taxable = $1,050
        // $1,050 ≤ $1,313, entirely in 0% bracket → $0
        var result = Calculate(GrossWages: 1_400m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Single filer — top bracket ────────────────────────────────────

    [Fact]
    public void Single_Biweekly_TopBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // less std ded (single) = $15,750 → taxable = $62,250
        // 2%  on $1,313–$2,626:   $1,313 × 0.020 = $26.260
        // 2.5% on $2,626–$3,939:  $1,313 × 0.025 = $32.825
        // 3%  on $3,939–$5,252:   $1,313 × 0.030 = $39.390
        // 3.5% on $5,252–$6,565:  $1,313 × 0.035 = $45.955
        // 4%  on $6,565–$7,878:   $1,313 × 0.040 = $52.520
        // 4.5% on $7,878–$9,191:  $1,313 × 0.045 = $59.085
        // 4.7% on $9,191–$62,250: $53,059 × 0.047 = $2,493.773
        // annual tax = $2,749.808
        // per period = $2,749.808 / 26 = $105.761846... → $105.76
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(105.76m, result.Withholding);
    }

    // ── Single filer — annual wages below standard deduction ─────────

    [Fact]
    public void Single_Biweekly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $500 × 26 = $13,000
        // less std ded (single) = $15,750 → taxable = max(0, -$2,750) = $0
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married filer — straddles several brackets ────────────────────

    [Fact]
    public void Married_Monthly_StraddlesSeveralBrackets()
    {
        // annual = $3,000 × 12 = $36,000
        // less std ded (married) = $31,500 → taxable = $4,500
        // 2%  on $1,313–$2,626:  $1,313 × 0.020 = $26.260
        // 2.5% on $2,626–$3,939: $1,313 × 0.025 = $32.825
        // 3%  on $3,939–$4,500:  $561  × 0.030 = $16.830
        // annual tax = $75.915
        // per period = $75.915 / 12 = $6.32625 → $6.33
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(6.33m, result.Withholding);
    }

    // ── Married filer — top bracket ───────────────────────────────────

    [Fact]
    public void Married_Biweekly_TopBracket()
    {
        // annual = $5,000 × 26 = $130,000
        // less std ded (married) = $31,500 → taxable = $98,500
        // 2%  on $1,313–$2,626:  $1,313 × 0.020 = $26.260
        // 2.5% on $2,626–$3,939: $1,313 × 0.025 = $32.825
        // 3%  on $3,939–$5,252:  $1,313 × 0.030 = $39.390
        // 3.5% on $5,252–$6,565: $1,313 × 0.035 = $45.955
        // 4%  on $6,565–$7,878:  $1,313 × 0.040 = $52.520
        // 4.5% on $7,878–$9,191: $1,313 × 0.045 = $59.085
        // 4.7% on $9,191–$98,500: $89,309 × 0.047 = $4,197.523
        // annual tax = $4,453.558
        // per period = $4,453.558 / 26 = $171.290692... → $171.29
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(171.29m, result.Withholding);
    }

    // ── Head of Household — straddles several brackets ────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_StraddlesSeveralBrackets()
    {
        // annual = $2,500 × 12 = $30,000
        // less std ded (HoH) = $23,625 → taxable = $6,375
        // 2%  on $1,313–$2,626:  $1,313 × 0.020 = $26.260
        // 2.5% on $2,626–$3,939: $1,313 × 0.025 = $32.825
        // 3%  on $3,939–$5,252:  $1,313 × 0.030 = $39.390
        // 3.5% on $5,252–$6,375: $1,123 × 0.035 = $39.305
        // annual tax = $137.780
        // per period = $137.780 / 12 = $11.48166... → $11.48
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(11.48m, result.Withholding);
    }

    // ── Head of Household — top bracket ──────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_TopBracket()
    {
        // annual = $4,000 × 26 = $104,000
        // less std ded (HoH) = $23,625 → taxable = $80,375
        // 2%  on $1,313–$2,626:  $1,313 × 0.020 = $26.260
        // 2.5% on $2,626–$3,939: $1,313 × 0.025 = $32.825
        // 3%  on $3,939–$5,252:  $1,313 × 0.030 = $39.390
        // 3.5% on $5,252–$6,565: $1,313 × 0.035 = $45.955
        // 4%  on $6,565–$7,878:  $1,313 × 0.040 = $52.520
        // 4.5% on $7,878–$9,191: $1,313 × 0.045 = $59.085
        // 4.7% on $9,191–$80,375: $71,184 × 0.047 = $3,345.648
        // annual tax = $3,601.683
        // per period = $3,601.683 / 26 = $138.52626... → $138.53
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(138.53m, result.Withholding);
    }

    // ── Head of Household — annual wages below standard deduction ────

    [Fact]
    public void HeadOfHousehold_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $1,900 × 12 = $22,800
        // less std ded (HoH) = $23,625 → taxable = max(0, -$825) = $0
        var result = Calculate(GrossWages: 1_900m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(0m, result.Withholding);
    }

    // ── MO W-4 Allowances ────────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_ReducesTax()
    {
        // annual = $3,000 × 26 = $78,000
        // less std ded (single) = $15,750
        // less 1 allowance     = $2,100
        // taxable = $60,150
        // ... brackets through $9,191 = $256.035
        // 4.7% on $9,191–$60,150: $50,959 × 0.047 = $2,395.073
        // annual tax = $2,651.108
        // per period = $2,651.108 / 26 = $101.96569... → $101.97
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(101.97m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReduceTaxFurther()
    {
        // annual = $3,000 × 26 = $78,000
        // less std ded (single) = $15,750
        // less 2 allowances    = $4,200
        // taxable = $58,050
        // ... brackets through $9,191 = $256.035
        // 4.7% on $9,191–$58,050: $48,859 × 0.047 = $2,296.373
        // annual tax = $2,552.408
        // per period = $2,552.408 / 26 = $98.169538... → $98.17
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(98.17m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base from HeadOfHousehold_Monthly_StraddlesSeveralBrackets = $11.48; extra = $25.00 → $36.48
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Monthly, "Head of Household",
            additionalWithholding: 25m);

        Assert.Equal(36.48m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // less std ded (single) = $15,750 → taxable = $49,250
        // ... brackets through $9,191 = $256.035
        // 4.7% on $9,191–$49,250: $40,059 × 0.047 = $1,882.773
        // annual tax = $2,138.808
        // per period = $2,138.808 / 26 = $82.2618... → $82.26
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(82.26m, result.Withholding);
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
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 2,
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
        int allowances = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new MissouriWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.MO,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["Allowances"] = allowances,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
