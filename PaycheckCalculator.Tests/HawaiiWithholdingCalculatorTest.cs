using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Hawaii;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for <see cref="HawaiiWithholdingCalculator"/>.
/// Expected dollar amounts are hand-computed from Hawaii Booklet A's
/// percentage-method formula:
///   annual taxable = max(0, (per-period wages × periods) − std deduction
///                              − ($1,144 × allowances))
///   annual tax     = graduated brackets (single or married) applied to
///                    annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
/// </summary>
public class HawaiiWithholdingCalculatorTest
{
    // ── State identity ─────────────────────────────────────────────

    [Fact]
    public void State_ReturnsHawaii()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        Assert.Equal(UsState.HI, calc.State);
    }

    // ── Schema ─────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_WithSingleAndMarriedOptions()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Equal("HW-4 Filing Status", field.Label);
        Assert.Equal(StateFieldType.Picker, field.FieldType);
        Assert.True(field.IsRequired);
        Assert.Equal(HawaiiWithholdingCalculator.StatusSingle, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(2, field.Options!.Count);
        Assert.Contains(HawaiiWithholdingCalculator.StatusSingle, field.Options);
        Assert.Contains(HawaiiWithholdingCalculator.StatusMarried, field.Options);
    }

    [Fact]
    public void Schema_ContainsAllowances()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Equal("HW-4 Allowances", field.Label);
        Assert.Equal(StateFieldType.Integer, field.FieldType);
        Assert.Equal(0, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Additional Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    // ── Validation ─────────────────────────────────────────────────

    [Theory]
    [InlineData(HawaiiWithholdingCalculator.StatusSingle)]
    [InlineData(HawaiiWithholdingCalculator.StatusMarried)]
    public void Validate_ValidFilingStatus_ReturnsNoErrors(string status)
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues { ["FilingStatus"] = status };
        Assert.Empty(calc.Validate(values));
    }

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues { ["FilingStatus"] = "Bogus" };
        var errors = calc.Validate(values);

        Assert.Contains(errors, e => e.Contains("Filing Status"));
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = -1
        };

        Assert.Contains(calc.Validate(values), e => e.Contains("Allowances"));
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = -5m
        };

        Assert.Contains(calc.Validate(values), e => e.Contains("Additional Withholding"));
    }

    // ── Calculation: Single filing status ──────────────────────────

    /// <summary>
    /// Single, biweekly $2,000, no allowances.
    /// annual wages       = 2,000 × 26            = 52,000
    /// annual taxable     = 52,000 − 2,200        = 49,800
    /// annual tax (single brackets through 49,800):
    ///     2,400 × 1.40% =    33.60
    ///     2,400 × 3.20% =    76.80
    ///     4,800 × 5.50% =   264.00
    ///     4,800 × 6.40% =   307.20
    ///     4,800 × 6.80% =   326.40
    ///     4,800 × 7.20% =   345.60
    ///    12,000 × 7.60% =   912.00
    ///    12,000 × 7.90% =   948.00
    ///     1,800 × 8.25% =   148.50
    ///                    = 3,362.10
    /// per-period         = 3,362.10 ÷ 26 = 129.3115… → 129.31
    /// </summary>
    [Fact]
    public void Calculate_Single_Biweekly_2000_NoAllowances()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(129.31m, result.Withholding);
    }

    /// <summary>
    /// Single, biweekly $2,000, 2 HW-4 allowances.
    /// annual taxable = 52,000 − 2,200 − 2×1,144 = 47,512
    /// annual tax (single, through 47,512):
    ///     0–24,000 sub-total                    = 1,353.60
    ///    12,000 × 7.60%                         =   912.00
    ///    11,512 × 7.90%                         =   909.448
    ///                                           = 3,175.048
    /// per-period = 3,175.048 ÷ 26 = 122.1172… → 122.12
    /// </summary>
    [Fact]
    public void Calculate_Single_TwoAllowances_ReducesWithholding()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 2
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(122.12m, result.Withholding);
    }

    /// <summary>
    /// Single, weekly $30 — below the standard deduction, so zero tax.
    /// annual wages = 30 × 52 = 1,560 < 2,200 std deduction → 0.
    /// </summary>
    [Fact]
    public void Calculate_Single_BelowStandardDeduction_ReturnsZero()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 30m,
            PayPeriod: PayFrequency.Weekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.Withholding);
    }

    /// <summary>
    /// Zero gross wages → zero withholding regardless of allowances.
    /// </summary>
    [Fact]
    public void Calculate_ZeroGrossWages_ReturnsZero()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 5
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    /// <summary>
    /// High earner reaches the 11% top bracket.
    /// Single, monthly $30,000, no allowances.
    /// annual wages = 360,000; taxable = 357,800
    /// annual tax:
    ///   0–48,000       sub-total         =  3,213.60
    ///   48k–150k × 8.25%                  =  8,415.00 → 11,628.60
    ///   150k–175k × 9.00%                 =  2,250.00 → 13,878.60
    ///   175k–300k × 10.00%                = 12,500.00 → 26,378.60
    ///   300k–357.8k × 11.00%              =  6,358.00 → 32,736.60
    /// per-period = 32,736.60 ÷ 12 = 2,728.05
    /// </summary>
    [Fact]
    public void Calculate_Single_HighEarner_ReachesTopBracket()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 30_000m,
            PayPeriod: PayFrequency.Monthly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(2_728.05m, result.Withholding);
    }

    // ── Calculation: Married filing status ─────────────────────────

    /// <summary>
    /// Married, biweekly $2,000, no allowances.
    /// annual taxable = 52,000 − 4,400 = 47,600
    /// annual tax (married brackets through 47,600):
    ///     4,800 × 1.40% =    67.20
    ///     4,800 × 3.20% =   153.60
    ///     9,600 × 5.50% =   528.00
    ///     9,600 × 6.40% =   614.40
    ///     9,600 × 6.80% =   652.80
    ///     9,200 × 7.20% =   662.40
    ///                    = 2,678.40
    /// per-period = 2,678.40 ÷ 26 = 103.0153… → 103.02
    /// </summary>
    [Fact]
    public void Calculate_Married_Biweekly_2000_NoAllowances()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusMarried,
            ["Allowances"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(103.02m, result.Withholding);
    }

    /// <summary>
    /// Married, biweekly $3,000, 3 HW-4 allowances.
    /// annual taxable = 78,000 − 4,400 − 3×1,144 = 70,168
    /// annual tax (married):
    ///     through 48,000   = 2,707.20
    ///    22,168 × 7.60%    = 1,684.768
    ///                      = 4,391.968
    /// per-period = 4,391.968 ÷ 26 = 168.9218… → 168.92
    /// </summary>
    [Fact]
    public void Calculate_Married_Biweekly_3000_ThreeAllowances()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 3_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusMarried,
            ["Allowances"] = 3
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(168.92m, result.Withholding);
    }

    // ── Additional withholding and pre-tax deductions ──────────────

    /// <summary>
    /// Extra HW-4 withholding is added on top of the per-period tax.
    /// Base Single $2,000 biweekly no allowances = 129.31 (see earlier test).
    /// With $50 additional per-period → 179.31.
    /// </summary>
    [Fact]
    public void Calculate_AdditionalWithholding_IsAdded()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 50m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(179.31m, result.Withholding);
    }

    /// <summary>
    /// Pre-tax deductions reduce state taxable wages.
    /// Single, biweekly gross $2,000 with $200 pre-tax deduction:
    ///   per-period taxable = 1,800; annual = 46,800
    ///   annual taxable     = 46,800 − 2,200 = 44,600
    /// annual tax (single): 0–36,000 = 2,265.60; 8,600 × 7.90% = 679.40
    ///   total = 2,945.00; per-period = 2,945 ÷ 26 = 113.269… → 113.27
    /// </summary>
    [Fact]
    public void Calculate_PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);
        var context = new CommonWithholdingContext(
            UsState.HI, GrossWages: 2_000m,
            PayPeriod: PayFrequency.Biweekly, Year: 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = HawaiiWithholdingCalculator.StatusSingle,
            ["Allowances"] = 0
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(1_800m, result.TaxableWages);
        Assert.Equal(113.27m, result.Withholding);
    }

    // ── TDI employee contribution ─────────────────────────────────────
    //
    // Hawaii DLIR Disability Compensation Division, 2026 Maximum Weekly Wage
    // Base (December 10, 2025): the employee share may not exceed 0.5% of
    // weekly wages nor the $7.50 maximum weekly deduction.
    // https://labor.hawaii.gov/dcd/files/2025/12/2026-Maximum-Weekly-Wage-Base.pdf

    [Fact]
    public void Tdi_BelowWeeklyCap_IsHalfOfOnePercent()
    {
        // Weekly $1,000 × 0.5% = $5.00, under the $7.50 weekly ceiling.
        var result = CalculateAssessments(grossWages: 1000m, frequency: PayFrequency.Weekly);

        Assert.Equal(5.00m, Assert.Single(result.TaxLines!, l => l.ShortCode == "TDI").Amount);
    }

    [Fact]
    public void Tdi_AboveWeeklyCap_IsCappedAtMaximumWeeklyDeduction()
    {
        // Weekly $3,000 × 0.5% = $15.00, capped to $7.50.
        var result = CalculateAssessments(grossWages: 3000m, frequency: PayFrequency.Weekly);

        Assert.Equal(7.50m, result.DisabilityInsurance);
    }

    [Fact]
    public void Tdi_WeeklyCapScalesWithPayFrequency()
    {
        // Biweekly caps at 2 × $7.50; semimonthly at 52/24 × $7.50 = $16.25.
        var biweekly = CalculateAssessments(grossWages: 10_000m, frequency: PayFrequency.Biweekly);
        var semimonthly = CalculateAssessments(grossWages: 10_000m, frequency: PayFrequency.Semimonthly);

        Assert.Equal(15.00m, biweekly.DisabilityInsurance);
        Assert.Equal(16.25m, semimonthly.DisabilityInsurance);
    }

    [Fact]
    public void Tdi_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        var result = CalculateAssessments(
            grossWages: 1000m, frequency: PayFrequency.Weekly, preTaxDeductions: 400m);

        Assert.Equal(5.00m, result.DisabilityInsurance);
    }

    [Fact]
    public void Tdi_EmployerPaysFullPremium_SuppressesDeduction()
    {
        // Hawaii lets the employer carry the whole cost rather than share it.
        var result = CalculateAssessments(
            grossWages: 1000m, frequency: PayFrequency.Weekly, employerPays: true);

        Assert.Equal(0m, result.DisabilityInsurance);
        Assert.DoesNotContain(result.TaxLines!, l => l.Kind == StateTaxLineKind.PayrollAssessment);
    }

    [Fact]
    public void Tdi_RoundsAwayFromZero()
    {
        // Weekly $101 × 0.5% = 0.505 exactly — a true midpoint, so away-from-zero
        // gives 0.51 where banker's rounding would give 0.50.
        var result = CalculateAssessments(grossWages: 101m, frequency: PayFrequency.Weekly);

        Assert.Equal(0.51m, result.DisabilityInsurance);
    }

    private static StateWithholdingResult CalculateAssessments(
        decimal grossWages,
        PayFrequency frequency = PayFrequency.Biweekly,
        decimal preTaxDeductions = 0m,
        decimal ytdStateWages = 0m,
        bool employerPays = false)
    {{
        var calc = new HawaiiWithholdingCalculator(TestSchemas.Provider, TestAssessments.Table);

        var context = new CommonWithholdingContext(
            UsState.HI,
            GrossWages: grossWages,
            PayPeriod: frequency,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions,
            YtdStateWages: ytdStateWages);

        var values = PayCalculatorTestHarness.DefaultValues(TestSchemas.Provider.GetSchema(UsState.HI));
        values["EmployerPaysTdi"] = employerPays;

        return calc.Calculate(context, values);
    }}

}
