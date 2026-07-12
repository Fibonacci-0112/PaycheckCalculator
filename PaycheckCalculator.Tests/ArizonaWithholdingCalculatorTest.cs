using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Arizona;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

public class ArizonaWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsArizona()
    {
        var calc = new ArizonaWithholdingCalculator();
        Assert.Equal(UsState.AZ, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsWithholdingRatePicker_WithSevenA4Options()
    {
        var calc = new ArizonaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "WithholdingRate");
        Assert.Equal("A-4 Withholding Rate", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        // Form A-4 (Arizona Department of Revenue) lists exactly seven
        // percentage elections: 0.5%, 1.0%, 1.5%, 2.0%, 2.5%, 3.0%, 3.5%.
        Assert.NotNull(field.Options);
        Assert.Equal(
            new[] { "0.5%", "1.0%", "1.5%", "2.0%", "2.5%", "3.0%", "3.5%" },
            field.Options);
        // Arizona default when no A-4 is on file is 2.0%.
        Assert.Equal("2.0%", field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsExtraWithholding()
    {
        var calc = new ArizonaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    [Fact]
    public void Schema_DoesNotExposeFilingStatusOrAllowances()
    {
        // Arizona's A-4 percentage election does not use filing status,
        // allowances, or dependents for per-period withholding — those
        // reconcile on Form 140.
        var calc = new ArizonaWithholdingCalculator();
        var schema = calc.GetInputSchema();

        Assert.DoesNotContain(schema, f => f.Key == "FilingStatus");
        Assert.DoesNotContain(schema, f => f.Key == "Allowances");
        Assert.DoesNotContain(schema, f => f.Key == "Dependents");
    }

    // ── Validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("0.5%")]
    [InlineData("1.0%")]
    [InlineData("1.5%")]
    [InlineData("2.0%")]
    [InlineData("2.5%")]
    [InlineData("3.0%")]
    [InlineData("3.5%")]
    public void Validate_KnownA4Rate_ReturnsNoErrors(string rate)
    {
        var calc = new ArizonaWithholdingCalculator();
        var values = new StateInputValues { ["WithholdingRate"] = rate };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_UnknownRate_ReturnsError()
    {
        var calc = new ArizonaWithholdingCalculator();
        var values = new StateInputValues { ["WithholdingRate"] = "4.5%" };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("A-4 Withholding Rate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NegativeExtraWithholding_ReturnsError()
    {
        var calc = new ArizonaWithholdingCalculator();
        var values = new StateInputValues
        {
            ["WithholdingRate"] = "2.0%",
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Extra Withholding", StringComparison.OrdinalIgnoreCase));
    }

    // ── Form A-4 rate table ─────────────────────────────────────────
    // Expected values computed directly from the Form A-4 percentages:
    //   withholding = taxable wages × rate, rounded to cents.

    [Theory]
    [InlineData("0.5%",  5.00)]
    [InlineData("1.0%", 10.00)]
    [InlineData("1.5%", 15.00)]
    [InlineData("2.0%", 20.00)]
    [InlineData("2.5%", 25.00)]
    [InlineData("3.0%", 30.00)]
    [InlineData("3.5%", 35.00)]
    public void A4Rate_AppliedDirectlyToTaxableWages(string rate, double expected)
    {
        // Round numbers ($1,000 biweekly) make the expected withholding
        // trivially verifiable: 1000 × rate.
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WithholdingRate"] = rate };

        var result = calc.Calculate(context, values);

        Assert.Equal(1000m, result.TaxableWages);
        Assert.Equal((decimal)expected, result.Withholding);
    }

    [Fact]
    public void A4Rate_RoundsToCents_AwayFromZero()
    {
        // $1,234.57 × 2.5% = 30.86425 → rounds away-from-zero to $30.86.
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 1234.57m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WithholdingRate"] = "2.5%" };

        var result = calc.Calculate(context, values);

        Assert.Equal(30.86m, result.Withholding);
    }

    // ── Default election (no A-4 on file) ───────────────────────────

    [Fact]
    public void MissingRate_DefaultsTo2Percent()
    {
        // Arizona employers must withhold at 2.0% when an employee has
        // not filed a valid A-4.  The calculator mirrors this behavior
        // when the dictionary has no WithholdingRate key.
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 2500m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // 2500 × 0.020 = 50.00
        Assert.Equal(2500m, result.TaxableWages);
        Assert.Equal(50.00m, result.Withholding);
    }

    [Fact]
    public void UnknownRate_FallsBackTo2Percent()
    {
        // Defensive fallback: a corrupted/unknown picker value must not
        // throw; instead it falls back to the Arizona default of 2.0%.
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 2500m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WithholdingRate"] = "bogus" };

        var result = calc.Calculate(context, values);

        Assert.Equal(50.00m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceStateTaxableWages()
    {
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues { ["WithholdingRate"] = "2.5%" };

        var result = calc.Calculate(context, values);

        // State wages = 2000 − 200 = 1800
        // Withholding = 1800 × 0.025 = 45.00
        Assert.Equal(1800m, result.TaxableWages);
        Assert.Equal(45.00m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 500m,
            PayPeriod: PayFrequency.Weekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 800m);
        var values = new StateInputValues { ["WithholdingRate"] = "3.5%" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Extra withholding ───────────────────────────────────────────

    [Fact]
    public void ExtraWithholding_IsAddedAfterTaxCalc()
    {
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["WithholdingRate"] = "2.0%",
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // 2000 × 0.020 = 40.00, plus $25 extra = $65.00
        Assert.Equal(65.00m, result.Withholding);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["WithholdingRate"] = "3.5%" };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_PreTaxAndExtraWithholding()
    {
        var calc = new ArizonaWithholdingCalculator();

        // Semimonthly employee: $3,000 gross, $250 pre-tax, 3.0% A-4
        // election, $10 extra per pay period.
        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Semimonthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 250m);
        var values = new StateInputValues
        {
            ["WithholdingRate"] = "3.0%",
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        // State wages = 3000 − 250 = 2750
        // Withholding = 2750 × 0.030 = 82.50, plus $10 = $92.50
        Assert.Equal(2750m, result.TaxableWages);
        Assert.Equal(92.50m, result.Withholding);
    }

    // ── No disability insurance for Arizona ─────────────────────────

    [Fact]
    public void NoDisabilityInsurance()
    {
        var calc = new ArizonaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.AZ,
            GrossWages: 2000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues { ["WithholdingRate"] = "2.0%" });

        Assert.Equal(0m, result.DisabilityInsurance);
    }
}
