using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Kentucky;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class KentuckyWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsKentucky()
    {
        var calc = new KentuckyWithholdingCalculator();
        Assert.Equal(UsState.KY, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsAllowances()
    {
        var calc = new KentuckyWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Equal("K-4 Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new KentuckyWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    [Fact]
    public void Schema_DoesNotExposeFilingStatus()
    {
        // Kentucky's 2026 Form 42A003 formula does not distinguish between
        // filing statuses — the $3,160 standard deduction applies uniformly.
        var calc = new KentuckyWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.DoesNotContain(schema, f => f.Key == "FilingStatus");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultValues_ReturnsEmpty()
    {
        var calc = new KentuckyWithholdingCalculator();
        Assert.Empty(calc.Validate(new StateInputValues()));
    }

    [Fact]
    public void Validate_PositiveAllowances_ReturnsEmpty()
    {
        var calc = new KentuckyWithholdingCalculator();
        var values = new StateInputValues { ["Allowances"] = 5 };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new KentuckyWithholdingCalculator();
        var values = new StateInputValues { ["Allowances"] = -1 };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Allowances", StringComparison.OrdinalIgnoreCase));
    }

    // ── 2026 Form 42A003 flat-rate calculation ──────────────────────

    [Fact]
    public void Biweekly_NoAllowances_FlatRateAfterStandardDeduction()
    {
        // Biweekly employee, $3,000 gross, no K-4 allowances.
        // Annual wages     = $3,000 × 26 = $78,000
        // Annual taxable   = $78,000 − $3,160 = $74,840
        // Annual tax       = $74,840 × 4.0% = $2,993.60
        // Per-period tax   = $2,993.60 / 26 = $115.1384615... → $115.14
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(115.14m, result.Withholding);
    }

    [Fact]
    public void Biweekly_TwoAllowances_CreditsReduceWithholding()
    {
        // Same as above, but 2 K-4 allowances → $20 annual credit.
        // Annual tax     = $2,993.60
        // Annual credit  = 2 × $10 = $20
        // After credit   = $2,973.60
        // Per-period     = $2,973.60 / 26 = $114.3692307... → $114.37
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["Allowances"] = 2 };

        var result = calc.Calculate(context, values);

        Assert.Equal(3000m, result.TaxableWages);
        Assert.Equal(114.37m, result.Withholding);
    }

    [Fact]
    public void Weekly_NoAllowances_StandardDeductionAnnualizedCorrectly()
    {
        // Weekly employee, $1,000 gross, no allowances.
        // Annual wages   = $1,000 × 52 = $52,000
        // Annual taxable = $52,000 − $3,160 = $48,840
        // Annual tax     = $48,840 × 4.0% = $1,953.60
        // Per-period     = $1,953.60 / 52 = $37.5692307... → $37.57
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(1000m, result.TaxableWages);
        Assert.Equal(37.57m, result.Withholding);
    }

    [Fact]
    public void Monthly_NoAllowances_HighEarner()
    {
        // Monthly employee, $5,000 gross, no allowances.
        // Annual wages   = $5,000 × 12 = $60,000
        // Annual taxable = $60,000 − $3,160 = $56,840
        // Annual tax     = $56,840 × 4.0% = $2,273.60
        // Per-period     = $2,273.60 / 12 = $189.4666... → $189.47
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(189.47m, result.Withholding);
    }

    // ── Standard deduction boundary ─────────────────────────────────

    [Fact]
    public void LowWages_StandardDeductionExceedsAnnualWages_ZeroWithholding()
    {
        // Weekly employee earning $50/week.
        // Annual wages   = $50 × 52 = $2,600 < $3,160 standard deduction
        // Annual taxable = max(0, $2,600 − $3,160) = $0
        // Withholding    = $0
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 50m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(50m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── K-4 allowance credit floor ──────────────────────────────────

    [Fact]
    public void LargeAllowances_CreditExceedsTax_FloorAtZero()
    {
        // Employee whose K-4 credits exceed computed tax.
        // Biweekly $500 gross, 100 allowances.
        // Annual wages   = $500 × 26 = $13,000
        // Annual taxable = $13,000 − $3,160 = $9,840
        // Annual tax     = $9,840 × 4.0% = $393.60
        // Annual credit  = 100 × $10 = $1,000 > $393.60
        // After credit   = max(0, $393.60 − $1,000) = $0
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["Allowances"] = 100 };

        var result = calc.Calculate(context, values);

        Assert.Equal(500m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceStateTaxableWages()
    {
        // Biweekly $3,000 gross, $500 pre-tax deduction (e.g. 401k).
        // State taxable wages = $3,000 − $500 = $2,500
        // Annual wages   = $2,500 × 26 = $65,000
        // Annual taxable = $65,000 − $3,160 = $61,840
        // Annual tax     = $61,840 × 4.0% = $2,473.60
        // Per-period     = $2,473.60 / 26 = $95.1384615... → $95.14
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(2500m, result.TaxableWages);
        Assert.Equal(95.14m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAddedAfterTaxCalc()
    {
        // Biweekly $3,000, no allowances; base = $115.14, plus $25 extra.
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        Assert.Equal(140.14m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_AllowancesPreTaxAndExtraWithholding()
    {
        // Semimonthly employee: $4,500 gross, $500 pre-tax (401k),
        // 3 K-4 allowances, $10 extra withholding per K-4 Line 3.
        // State wages      = $4,500 − $500 = $4,000
        // Annual wages     = $4,000 × 24 = $96,000
        // Annual taxable   = $96,000 − $3,160 = $92,840
        // Annual tax       = $92,840 × 4.0% = $3,713.60
        // Annual credit    = 3 × $10 = $30
        // After credit     = $3,713.60 − $30 = $3,683.60
        // Per-period       = $3,683.60 / 24 = $153.4833... → $153.48
        // Total            = $153.48 + $10 = $163.48
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 4500m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["Allowances"] = 3,
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(163.48m, result.Withholding);
    }

    [Fact]
    public void Annual_PayFrequency_FullDeductionApplied()
    {
        // Annual payroll employee: $70,000 annual gross, 1 allowance.
        // State wages    = $70,000
        // Annual taxable = $70,000 − $3,160 = $66,840
        // Annual tax     = $66,840 × 4.0% = $2,673.60
        // Annual credit  = 1 × $10 = $10
        // After credit   = $2,673.60 − $10 = $2,663.60
        // Per-period     = $2,663.60 / 1 = $2,663.60
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 70000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues { ["Allowances"] = 1 };

        var result = calc.Calculate(context, values);

        Assert.Equal(70000m, result.TaxableWages);
        Assert.Equal(2663.60m, result.Withholding);
    }

    // ── No disability insurance for Kentucky ────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new KentuckyWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.KY,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.DisabilityInsurance);
    }
}
