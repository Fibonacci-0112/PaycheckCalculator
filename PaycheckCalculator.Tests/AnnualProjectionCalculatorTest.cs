using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="AnnualProjectionCalculator"/>, verifying annualized
/// amounts, projected YTD by paycheck number, remaining paychecks, and
/// estimated over/under withholding.
/// </summary>
public sealed class AnnualProjectionCalculatorTest
{
    [Fact]
    public void Projection_DisclosesWithholdingProxyAssumptionsAndExclusions()
    {
        var (projection, _) = RunProjection();

        Assert.Contains(projection.AccuracyNotes, note => note.Description.Contains("Form 1040", StringComparison.Ordinal));
        Assert.Contains(projection.AccuracyNotes, note => note.Description.Contains("state withholding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(projection.AccuracyNotes, note => note.Description.Contains("$200,000", StringComparison.Ordinal));
        Assert.Contains(projection.AccuracyNotes, note => note.Description.Contains("local taxes", StringComparison.OrdinalIgnoreCase));
    }

    // ── Annualized amounts ──────────────────────────────────

    [Fact]
    public void Biweekly_AnnualizedGrossPay_Is26TimesPerPeriod()
    {
        // Gross: 40 hrs × $50/hr = $2,000 per period
        // Annualized: $2,000 × 26 = $52,000
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(52_000m, projection.AnnualizedGrossPay);
        Assert.Equal(26, projection.PayPeriodsPerYear);
    }

    [Fact]
    public void Weekly_AnnualizedGrossPay_Is52TimesPerPeriod()
    {
        // Gross: 40 hrs × $25/hr = $1,000 per period
        // Annualized: $1,000 × 52 = $52,000
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Weekly,
            hourlyRate: 25m,
            regularHours: 40m);

        Assert.Equal(52_000m, projection.AnnualizedGrossPay);
        Assert.Equal(52, projection.PayPeriodsPerYear);
    }

    [Fact]
    public void Monthly_AnnualizedGrossPay_Is12TimesPerPeriod()
    {
        // Gross: 160 hrs × $30/hr = $4,800 per period
        // Annualized: $4,800 × 12 = $57,600
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Monthly,
            hourlyRate: 30m,
            regularHours: 160m);

        Assert.Equal(57_600m, projection.AnnualizedGrossPay);
        Assert.Equal(12, projection.PayPeriodsPerYear);
    }

    [Fact]
    public void AnnualizedFederalTaxableWages_EqualsPerPeriodTimesPeriods()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(
            Math.Round(result.FederalTaxableIncome * 26, 2, MidpointRounding.AwayFromZero),
            projection.AnnualizedFederalTaxableWages);
    }

    [Fact]
    public void AnnualizedStateTaxableWages_EqualsPerPeriodTimesPeriods()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(
            Math.Round(result.StateTaxableWages * 26, 2, MidpointRounding.AwayFromZero),
            projection.AnnualizedStateTaxableWages);
    }

    [Fact]
    public void AnnualizedFica_EqualsPerPeriodFicaTimesPeriods()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        var perPeriodFica = result.SocialSecurityWithholding
            + result.MedicareWithholding
            + result.AdditionalMedicareWithholding;

        Assert.Equal(
            Math.Round(perPeriodFica * 26, 2, MidpointRounding.AwayFromZero),
            projection.AnnualizedFica);
    }

    [Fact]
    public void AnnualizedNetPay_EqualsPerPeriodTimesPeriods()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(
            Math.Round(result.NetPay * 26, 2, MidpointRounding.AwayFromZero),
            projection.AnnualizedNetPay);
    }

    // ── Projected YTD ───────────────────────────────────────

    [Fact]
    public void ProjectedYtd_Paycheck10_Is10TimesPerPeriod()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 10);

        Assert.Equal(10, projection.CurrentPaycheckNumber);
        Assert.Equal(
            Math.Round(result.GrossPay * 10, 2, MidpointRounding.AwayFromZero),
            projection.ProjectedYtdGrossPay);
        Assert.Equal(
            Math.Round(result.NetPay * 10, 2, MidpointRounding.AwayFromZero),
            projection.ProjectedYtdNetPay);
    }

    [Fact]
    public void ProjectedYtd_Paycheck1_EqualsOnePerPeriod()
    {
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 1);

        Assert.Equal(1, projection.CurrentPaycheckNumber);
        Assert.Equal(result.GrossPay, projection.ProjectedYtdGrossPay);
        Assert.Equal(result.NetPay, projection.ProjectedYtdNetPay);
    }

    // ── Remaining paychecks ─────────────────────────────────

    [Fact]
    public void RemainingPaychecks_BiweeklyPaycheck10_Is16()
    {
        // 26 periods - 10 = 16 remaining
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 10);

        Assert.Equal(16, projection.RemainingPaychecks);
    }

    [Fact]
    public void RemainingPaychecks_LastPaycheck_IsZero()
    {
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 26);

        Assert.Equal(0, projection.RemainingPaychecks);
    }

    [Fact]
    public void RemainingPaychecks_MonthlyPaycheck6_Is6()
    {
        // 12 periods - 6 = 6 remaining
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Monthly,
            hourlyRate: 30m,
            regularHours: 160m,
            paycheckNumber: 6);

        Assert.Equal(6, projection.RemainingPaychecks);
    }

    // ── Paycheck number clamping ────────────────────────────

    [Fact]
    public void PaycheckNumber_ClampedToMax_WhenExceedsPeriods()
    {
        // Biweekly has 26 periods; paycheck 30 should clamp to 26
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 30);

        Assert.Equal(26, projection.CurrentPaycheckNumber);
        Assert.Equal(0, projection.RemainingPaychecks);
    }

    [Fact]
    public void PaycheckNumber_ClampedToMin_WhenZero()
    {
        // Paycheck 0 should clamp to 1
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            paycheckNumber: 0);

        Assert.Equal(1, projection.CurrentPaycheckNumber);
        Assert.Equal(25, projection.RemainingPaychecks);
    }

    // ── Over/under withholding estimate ─────────────────────

    [Fact]
    public void NoExtraWithholding_NoTaxState_OverUnderNearZero()
    {
        // TX has no state tax. With no Step4c extra withholding,
        // annualized withholding should closely match estimated liability.
        // Small rounding differences (a few cents) are expected because
        // per-period withholding is rounded before annualization.
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.InRange(projection.OverUnderWithholding, -1m, 1m);
    }

    [Fact]
    public void ExtraWithholding_ProducesOverWithholding()
    {
        // Adding Step 4(c) extra withholding inflates the annualized
        // withholding above the estimated liability, resulting in
        // a positive over-withholding (likely refund).
        // Small rounding variance from de-annualization is expected.
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            step4cExtra: 50m);  // $50 extra per paycheck

        // $50 × 26 = $1,300 in extra withholding (±$1 for rounding)
        Assert.True(projection.OverUnderWithholding > 0m,
            "Extra withholding should produce a positive over-withholding amount");
        Assert.InRange(projection.OverUnderWithholding, 1_299m, 1_301m);
    }

    [Fact]
    public void AnnualizedTotalWithholding_SumsAllComponents()
    {
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(
            projection.AnnualizedFederalWithholding
                + projection.AnnualizedStateWithholding
                + projection.AnnualizedFica,
            projection.AnnualizedTotalWithholding);
    }

    [Fact]
    public void EstimatedFicaLiability_RespectsWageBase()
    {
        // $200/hr × 40 hrs = $8,000 per period
        // Annualized: $8,000 × 26 = $208,000 (above $184,500 SS wage base)
        // SS liability: $184,500 × 6.2% = $11,439
        // Medicare liability: $208,000 × 1.45% = $3,016
        // Additional Medicare: ($208,000 - $200,000) × 0.9% = $72
        // Total FICA liability: $11,439 + $3,016 + $72 = $14,527
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 200m,
            regularHours: 40m);

        Assert.Equal(14_527m, projection.EstimatedAnnualFicaLiability);
    }

    [Fact]
    public void EstimatedFicaLiability_BelowWageBase_NoAdditionalMedicare()
    {
        // $25/hr × 40 hrs = $1,000 per period
        // Annualized: $1,000 × 26 = $26,000 (well below SS cap and Medicare threshold)
        // SS: $26,000 × 6.2% = $1,612
        // Medicare: $26,000 × 1.45% = $377
        // Additional Medicare: $0 (below $200,000)
        // Total: $1,989
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 25m,
            regularHours: 40m);

        Assert.Equal(1_989m, projection.EstimatedAnnualFicaLiability);
    }

    // ── All pay frequencies produce valid projections ────────

    [Theory]
    [InlineData(PayFrequency.Weekly, 52)]
    [InlineData(PayFrequency.Biweekly, 26)]
    [InlineData(PayFrequency.Semimonthly, 24)]
    [InlineData(PayFrequency.Monthly, 12)]
    [InlineData(PayFrequency.Quarterly, 4)]
    [InlineData(PayFrequency.Semiannual, 2)]
    [InlineData(PayFrequency.Annual, 1)]
    [InlineData(PayFrequency.Daily, 260)]
    [InlineData(PayFrequency.Weekly53, 53)]
    [InlineData(PayFrequency.Biweekly27, 27)]
    public void AllFrequencies_ProduceValidProjections(PayFrequency freq, int expectedPeriods)
    {
        var (projection, result) = RunProjection(
            frequency: freq,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(expectedPeriods, projection.PayPeriodsPerYear);
        Assert.Equal(
            Math.Round(result.GrossPay * expectedPeriods, 2, MidpointRounding.AwayFromZero),
            projection.AnnualizedGrossPay);
        Assert.True(projection.AnnualizedNetPay > 0m);
    }

    // ── Annualized deductions ─────────────────────────────────

    [Fact]
    public void AnnualizedPreTaxDeductions_IsZero_WhenNoDeductions()
    {
        var (projection, _) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m);

        Assert.Equal(0m, projection.AnnualizedPreTaxDeductions);
        Assert.Equal(0m, projection.AnnualizedPostTaxDeductions);
    }

    [Fact]
    public void AnnualizedPreTaxDeductions_Biweekly_Is26TimesPerPeriod()
    {
        // $200 pre-tax per period × 26 = $5,200 annualized
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            preTaxDeduction: 200m);

        Assert.Equal(200m, result.PreTaxDeductions);
        Assert.Equal(5_200m, projection.AnnualizedPreTaxDeductions);
    }

    [Fact]
    public void AnnualizedPostTaxDeductions_Biweekly_Is26TimesPerPeriod()
    {
        // $100 post-tax per period × 26 = $2,600 annualized
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Biweekly,
            hourlyRate: 50m,
            regularHours: 40m,
            postTaxDeduction: 100m);

        Assert.Equal(100m, result.PostTaxDeductions);
        Assert.Equal(2_600m, projection.AnnualizedPostTaxDeductions);
    }

    [Fact]
    public void AnnualizedDeductions_BothPreAndPostTax_Monthly()
    {
        // Gross: 160 hrs × $30/hr = $4,800 per period
        // $300 pre-tax per period × 12 = $3,600 annualized
        // $150 post-tax per period × 12 = $1,800 annualized
        var (projection, result) = RunProjection(
            frequency: PayFrequency.Monthly,
            hourlyRate: 30m,
            regularHours: 160m,
            preTaxDeduction: 300m,
            postTaxDeduction: 150m);

        Assert.Equal(300m, result.PreTaxDeductions);
        Assert.Equal(150m, result.PostTaxDeductions);
        Assert.Equal(3_600m, projection.AnnualizedPreTaxDeductions);
        Assert.Equal(1_800m, projection.AnnualizedPostTaxDeductions);
    }

    // ── Helpers ─────────────────────────────────────────────

    private static (AnnualProjection projection, PaycheckResult result) RunProjection(
        PayFrequency frequency,
        decimal hourlyRate,
        decimal regularHours,
        int paycheckNumber = 1,
        decimal step4cExtra = 0m,
        decimal preTaxDeduction = 0m,
        decimal postTaxDeduction = 0m)
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        var payCalc = new PayCalculator(registry, fica, fed);
        var projCalc = new AnnualProjectionCalculator(fed, fica);

        var deductions = new List<Deduction>();
        if (preTaxDeduction > 0m)
            deductions.Add(new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = preTaxDeduction });
        if (postTaxDeduction > 0m)
            deductions.Add(new Deduction { Name = "Roth 401k", Type = DeductionType.PostTax, Amount = postTaxDeduction });

        var input = new PaycheckInput
        {
            Frequency = frequency,
            HourlyRate = hourlyRate,
            RegularHours = regularHours,
            State = UsState.TX,
            FederalW4 = new FederalW4Input
            {
                Step4cExtraWithholding = step4cExtra
            },
            PaycheckNumber = paycheckNumber,
            Deductions = deductions
        };

        var result = payCalc.Calculate(input);
        var projection = projCalc.Calculate(input, result);

        return (projection, result);
    }
}
