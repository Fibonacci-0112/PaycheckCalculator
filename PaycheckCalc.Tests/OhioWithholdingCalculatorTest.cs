using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Ohio;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for Ohio (OH) state income tax withholding.
/// Ohio uses the dedicated <see cref="OhioWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the Ohio Department of
/// Taxation "Optional Computer Formula" (2026 Employer Withholding Tax
/// Filing Guidelines):
///
///   annual wages       = (per-period wages − pre-tax deductions) × periods
///   annual exemption   = $650 × IT-4 exemptions
///   annual taxable     = max(0, annual wages − annual exemption)
///   annual tax         = 0                                      if taxable ≤ $26,050
///                      = (taxable − $26,050) × 2.75%            if taxable > $26,050
///   per-period wh      = round(annual tax ÷ periods, 2) + extra withholding
///
/// Ohio does not use filing status for withholding; only the exemption
/// count, pre-tax deductions, and per-period extra withholding vary.
/// </summary>
public class OhioWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsOhio()
    {
        var calc = new OhioWithholdingCalculator();
        Assert.Equal(UsState.OH, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsExemptions_AdditionalWithholding()
    {
        var calc = new OhioWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.Equal(2, schema.Count);
        Assert.Single(schema, f => f.Key == "Exemptions");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_Exemptions_IsIntegerField_DefaultZero()
    {
        var calc = new OhioWithholdingCalculator();
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "Exemptions");

        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_DoesNotIncludeFilingStatus()
    {
        // Ohio's IT-4 and withholding formula do not branch on filing status.
        var calc = new OhioWithholdingCalculator();
        Assert.DoesNotContain(calc.GetInputSchema(), f => f.Key == "FilingStatus");
    }

    // ── Zero-bracket (below $26,050 annualized) ─────────────────────

    [Fact]
    public void Biweekly_LowIncome_BelowZeroBracketCeiling_ReturnsZero()
    {
        // annual = $900 × 26 = $23,400; taxable = $23,400 ≤ $26,050 → 0%
        var result = Calculate(GrossWages: 900m, PayFrequency.Biweekly);

        Assert.Equal(900m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Monthly_LowIncome_BelowZeroBracketCeiling_ReturnsZero()
    {
        // annual = $2,000 × 12 = $24,000; taxable = $24,000 ≤ $26,050 → 0%
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Monthly);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Above-threshold withholding, no exemptions ──────────────────

    [Fact]
    public void Biweekly_AboveThreshold_NoExemptions()
    {
        // annual = $3,000 × 26 = $78,000
        // annual taxable = $78,000 (no exemptions)
        // tax = ($78,000 − $26,050) × 2.75% = $51,950 × 0.0275 = $1,428.625
        // per period = $1,428.625 / 26 = $54.9471... → $54.95
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(54.95m, result.Withholding);
    }

    [Fact]
    public void Monthly_AboveThreshold_NoExemptions()
    {
        // annual = $5,000 × 12 = $60,000
        // tax = ($60,000 − $26,050) × 2.75% = $33,950 × 0.0275 = $933.625
        // per period = $933.625 / 12 = $77.8020... → $77.80
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly);

        Assert.Equal(77.80m, result.Withholding);
    }

    [Fact]
    public void Weekly_AboveThreshold_NoExemptions()
    {
        // annual = $1,500 × 52 = $78,000
        // tax = ($78,000 − $26,050) × 2.75% = $51,950 × 0.0275 = $1,428.625
        // per period = $1,428.625 / 52 = $27.4735... → $27.47
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Weekly);

        Assert.Equal(27.47m, result.Withholding);
    }

    // ── IT-4 exemptions ─────────────────────────────────────────────

    [Fact]
    public void Biweekly_AboveThreshold_OneExemption_ReducesWithholding()
    {
        // annual = $3,000 × 26 = $78,000
        // annual exemption = 1 × $650 = $650
        // annual taxable = $78,000 − $650 = $77,350
        // tax = ($77,350 − $26,050) × 2.75% = $51,300 × 0.0275 = $1,410.75
        // per period = $1,410.75 / 26 = $54.2596... → $54.26
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, exemptions: 1);

        Assert.Equal(54.26m, result.Withholding);
    }

    [Fact]
    public void Biweekly_AboveThreshold_ThreeExemptions_ReducesWithholding()
    {
        // annual = $3,000 × 26 = $78,000
        // annual exemption = 3 × $650 = $1,950
        // annual taxable = $78,000 − $1,950 = $76,050
        // tax = ($76,050 − $26,050) × 2.75% = $50,000 × 0.0275 = $1,375.00
        // per period = $1,375.00 / 26 = $52.8846... → $52.88
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, exemptions: 3);

        Assert.Equal(52.88m, result.Withholding);
    }

    [Fact]
    public void Exemptions_CanReduceTaxableBelowZeroBracketCeiling()
    {
        // annual = $1,100 × 26 = $28,600
        // annual exemption = 5 × $650 = $3,250
        // annual taxable = $28,600 − $3,250 = $25,350 ≤ $26,050 → 0%
        var result = Calculate(GrossWages: 1_100m, PayFrequency.Biweekly, exemptions: 5);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Exemptions_AnnualExemption_CannotDriveTaxableNegative()
    {
        // annual = $100 × 26 = $2,600; annual exemption = 10 × $650 = $6,500
        // annual taxable = max(0, $2,600 − $6,500) = $0 → 0%
        var result = Calculate(GrossWages: 100m, PayFrequency.Biweekly, exemptions: 10);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Bracket boundary ────────────────────────────────────────────

    [Fact]
    public void AtZeroBracketCeiling_ExactBoundary_NoTax()
    {
        // annual taxable = exactly $26,050 → 0% (boundary is inclusive of zero bracket)
        // Use annual frequency so the arithmetic is exact.
        var result = Calculate(GrossWages: 26_050m, PayFrequency.Annual);

        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void JustAboveZeroBracketCeiling_TaxesOnlyTheExcess()
    {
        // annual taxable = $26,050 + $100 = $26,150
        // tax = $100 × 2.75% = $2.75 (annual, single pay period)
        var result = Calculate(GrossWages: 26_150m, PayFrequency.Annual);

        Assert.Equal(2.75m, result.Withholding);
    }

    // ── Additional withholding ──────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // Base biweekly (above threshold, no exemptions) = $54.95; extra $25 → $79.95
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly,
            additionalWithholding: 25m);

        Assert.Equal(79.95m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_AppliesEvenWhenBaseIsZero()
    {
        // Base = 0 (below threshold); extra $15 → $15
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly,
            additionalWithholding: 15m);

        Assert.Equal(15m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable per period = $2,500
        // annual = $2,500 × 26 = $65,000
        // tax = ($65,000 − $26,050) × 2.75% = $38,950 × 0.0275 = $1,071.125
        // per period = $1,071.125 / 26 = $41.1971... → $41.20
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(41.20m, result.Withholding);
    }

    // ── Zero gross wages ────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_NegativeExemptions_ReturnsError()
    {
        var calc = new OhioWithholdingCalculator();
        var values = new StateInputValues { ["Exemptions"] = -1 };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Exemptions"));
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new OhioWithholdingCalculator();
        var values = new StateInputValues
        {
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Additional Withholding"));
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new OhioWithholdingCalculator();
        var values = new StateInputValues
        {
            ["Exemptions"] = 2,
            ["AdditionalWithholding"] = 10m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_AnnualizedSteps_ShowBracketAndDeannualization()
    {
        // $2,000 biweekly, 0 exemptions:
        // annualize = 2,000 × 26 = 52,000
        // annual tax = (52,000 − 26,050) × 2.75% = 713.625
        // per period = 713.625 / 26 = 27.447... → 27.45
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly);

        Assert.NotNull(result.WithholdingSteps);
        var annualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annualize wages"));
        Assert.Equal(52_000m, annualizeStep.Value);
        var bracketStep = Assert.Single(result.WithholdingSteps!, s => s.Label.Contains("2.75%"));
        Assert.Equal(713.625m, bracketStep.Value);
        var deannualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "De-annualize to this pay period");
        Assert.Equal(27.45m, deannualizeStep.Value);
        Assert.Contains("IT-4", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_IncomeWithinZeroBracket_ShowsZeroBracketStep()
    {
        // $400 weekly → annual 20,800 ≤ 26,050 → 0% bracket
        var result = Calculate(GrossWages: 400m, PayFrequency.Weekly);

        Assert.Equal(0m, result.Withholding);
        var zeroStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Within Ohio's 0% bracket");
        Assert.Equal(0m, zeroStep.Value);
    }

    [Fact]
    public void Explanation_Exemptions_EmitExemptionAllowanceStep()
    {
        // 3 exemptions × $650 = $1,950 annual exemption allowance
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, exemptions: 3);

        var exemptionStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Less IT-4 exemption allowance");
        Assert.Equal(1_950m, exemptionStep.Value);
        // Annual taxable = 52,000 − 1,950 = 50,050
        var taxableStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual taxable income");
        Assert.Equal(50_050m, taxableStep.Value);
    }

    // ── Helper ──────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        int exemptions = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new OhioWithholdingCalculator();
        var context = new CommonWithholdingContext(
            UsState.OH,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["Exemptions"] = exemptions,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
