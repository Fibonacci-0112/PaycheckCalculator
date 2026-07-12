using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Nebraska;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for Nebraska (NE) state income tax withholding.
/// Nebraska uses the dedicated <see cref="NebraskaWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Nebraska Department of
/// Revenue annualized percentage-method formula (2026 Circular EN):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction)
///   annual tax     = graduated brackets applied to annual taxable
///   annual tax    -= allowances × $171  (allowance credit, not deduction)
///   annual tax     = max(0, annual tax)
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 Nebraska parameters:
///   Standard deduction: $8,600 (Single) / $17,200 (Married) / $12,900 (Head of Household)
///   Per-allowance credit (W-4N): $171 (applied to computed annual tax)
///   Brackets — Single / MFS:
///     2.46% on $0 – $4,030
///     3.51% on $4,030 – $24,120
///     5.01% on $24,120 – $38,870
///     5.2%  over $38,870
///   Brackets — Married / QSS:
///     2.46% on $0 – $8,040
///     3.51% on $8,040 – $48,250
///     5.01% on $48,250 – $77,730
///     5.2%  over $77,730
///   Brackets — Head of Household:
///     2.46% on $0 – $6,060
///     3.51% on $6,060 – $36,180
///     5.01% on $36,180 – $58,310
///     5.2%  over $58,310
/// </summary>
public class NebraskaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNebraska()
    {
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.NE, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_OptionsIncludeHeadOfHousehold()
    {
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
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
    public void Single_Biweekly_TwoBrackets()
    {
        // annual = $1,500 × 26 = $39,000
        // annual taxable = $39,000 − $8,600 = $30,400
        // tax = $4,030 × 2.46% + ($24,120 − $4,030) × 3.51% + ($30,400 − $24,120) × 5.01%
        //     = $99.138 + $705.159 + $314.628 = $1,118.925
        // per period = $1,118.925 / 26 = $43.035... → $43.04
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Biweekly, "Single");

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(43.04m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TopBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // annual taxable = $78,000 − $8,600 = $69,400
        // tax = $4,030 × 2.46% + ($24,120 − $4,030) × 3.51%
        //     + ($38,870 − $24,120) × 5.01% + ($69,400 − $38,870) × 5.2%
        //     = $99.138 + $705.159 + $738.975 + $1,587.56 = $3,130.832
        // per period = $3,130.832 / 26 = $120.416... → $120.42
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single");

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(120.42m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_SecondBracket()
    {
        // annual = $2,000 × 12 = $24,000
        // annual taxable = $24,000 − $8,600 = $15,400
        // tax = $4,030 × 2.46% + ($15,400 − $4,030) × 3.51%
        //     = $99.138 + $399.087 = $498.225
        // per period = $498.225 / 12 = $41.518... → $41.52
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(41.52m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $600 × 12 = $7,200
        // annual taxable = max(0, $7,200 − $8,600) = $0
        var result = Calculate(GrossWages: 600m, PayFrequency.Monthly, "Single");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Married filer ────────────────────────────────────────────────

    [Fact]
    public void Married_Biweekly_SecondBracket()
    {
        // annual = $2,500 × 26 = $65,000
        // annual taxable = $65,000 − $17,200 = $47,800
        // tax = $8,040 × 2.46% + ($47,800 − $8,040) × 3.51%
        //     = $197.784 + $1,395.576 = $1,593.360
        // per period = $1,593.360 / 26 = $61.283... → $61.28
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Biweekly, "Married");

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(61.28m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_SecondBracket()
    {
        // annual = $4,000 × 12 = $48,000
        // annual taxable = $48,000 − $17,200 = $30,800
        // tax = $8,040 × 2.46% + ($30,800 − $8,040) × 3.51%
        //     = $197.784 + $798.876 = $996.660
        // per period = $996.660 / 12 = $83.055 → $83.06
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, "Married");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(83.06m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_TopBracket()
    {
        // annual = $4,000 × 26 = $104,000
        // annual taxable = $104,000 − $17,200 = $86,800
        // tax = $8,040 × 2.46% + ($48,250 − $8,040) × 3.51%
        //     + ($77,730 − $48,250) × 5.01% + ($86,800 − $77,730) × 5.2%
        //     = $197.784 + $1,411.371 + $1,476.948 + $471.64 = $3,557.743
        // per period = $3,557.743 / 26 = $136.836... → $136.84
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly, "Married");

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(136.84m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_BelowStandardDeduction_ReturnsZero()
    {
        // annual = $600 × 26 = $15,600
        // annual taxable = max(0, $15,600 − $17,200) = $0
        var result = Calculate(GrossWages: 600m, PayFrequency.Biweekly, "Married");

        Assert.Equal(0m, result.Withholding);
    }

    // ── Head of Household filer ──────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_ThirdBracket()
    {
        // annual = $2,000 × 26 = $52,000
        // annual taxable = $52,000 − $12,900 = $39,100
        // tax = $6,060 × 2.46% + ($36,180 − $6,060) × 3.51%
        //     + ($39,100 − $36,180) × 5.01%
        //     = $149.076 + $1,057.212 + $146.292 = $1,352.580
        // per period = $1,352.580 / 26 = $52.022... → $52.02
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(52.02m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Monthly_SecondBracket()
    {
        // annual = $3,500 × 12 = $42,000
        // annual taxable = $42,000 − $12,900 = $29,100
        // tax = $6,060 × 2.46% + ($29,100 − $6,060) × 3.51%
        //     = $149.076 + $808.704 = $957.780
        // per period = $957.780 / 12 = $79.815 → $79.82
        var result = Calculate(GrossWages: 3_500m, PayFrequency.Monthly, "Head of Household");

        Assert.Equal(3_500m, result.TaxableWages);
        Assert.Equal(79.82m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_Biweekly_TopBracket()
    {
        // annual = $5,000 × 26 = $130,000
        // annual taxable = $130,000 − $12,900 = $117,100
        // tax = $6,060 × 2.46% + ($36,180 − $6,060) × 3.51%
        //     + ($58,310 − $36,180) × 5.01% + ($117,100 − $58,310) × 5.2%
        //     = $149.076 + $1,057.212 + $1,108.713 + $3,057.08 = $5,372.081
        // per period = $5,372.081 / 26 = $206.618... → $206.62
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Biweekly, "Head of Household");

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(206.62m, result.Withholding);
    }

    // ── W-4N Allowance credit ────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_OneAllowance_ReducesTax()
    {
        // Base annual tax (from $3,000 biweekly Single) = $3,130.832
        // minus 1 × $171 credit = $2,959.832
        // per period = $2,959.832 / 26 = $113.839... → $113.84
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 1);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(113.84m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReduceTaxFurther()
    {
        // Base annual tax = $3,130.832; minus 2 × $171 = $2,788.832
        // per period = $2,788.832 / 26 = $107.262... → $107.26
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single", allowances: 2);

        Assert.Equal(107.26m, result.Withholding);
    }

    [Fact]
    public void Allowances_EliminateAllTax_ReturnsZero()
    {
        // Claiming 20 allowances (credit = $3,420) on modest wages
        // zeroes out all computed tax before de-annualizing
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, "Single", allowances: 20);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ────────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // Base per-period (Single monthly $2,000) = $41.52; extra = $15 → $56.52
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, "Single",
            additionalWithholding: 15m);

        Assert.Equal(56.52m, result.Withholding);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000; annual taxable = $65,000 − $8,600 = $56,400
        // tax = $4,030 × 2.46% + ($24,120 − $4,030) × 3.51%
        //     + ($38,870 − $24,120) × 5.01% + ($56,400 − $38,870) × 5.2%
        //     = $99.138 + $705.159 + $738.975 + $911.56 = $2,454.832
        // per period = $2,454.832 / 26 = $94.416... → $94.42
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, "Single",
            preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(94.42m, result.Withholding);
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
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
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
        var calc = new NebraskaWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.NE,
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
