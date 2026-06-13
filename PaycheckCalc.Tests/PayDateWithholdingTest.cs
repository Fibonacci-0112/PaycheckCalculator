using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.Virginia;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// End-to-end tests that the anchor pay date changes withholding in 53-week / 27-biweekly years.
/// The resolved pay-period count flows from <see cref="PaycheckInput.PayDate"/> through
/// <see cref="PayCalculator"/> into both the federal calculator and the state calculators, so the
/// annualization factor (and therefore the withheld amount) differs from an ordinary 52/26 year.
/// </summary>
public sealed class PayDateWithholdingTest
{
    // 2026-01-01 is a Thursday → weekly schedules anchored here have 53 paychecks in 2026.
    private static readonly DateOnly ThursdayAnchor53 = new(2026, 1, 1);
    // 2026-01-02 is a Friday → ordinary 52-paycheck weekly schedule.
    private static readonly DateOnly FridayAnchor52 = new(2026, 1, 2);

    // 2026-01-01 aligns with the first biweekly date of 2026 → 27 paychecks.
    private static readonly DateOnly BiweeklyAnchor27 = new(2026, 1, 1);
    // 2026-01-08 is offset half a period → ordinary 26-paycheck biweekly schedule.
    private static readonly DateOnly BiweeklyAnchor26 = new(2026, 1, 8);

    [Fact]
    public void Federal_Weekly_53WeekAnchor_DiffersFrom52WeekAnchor()
    {
        var fed52 = CalculateFederal(PayFrequency.Weekly, FridayAnchor52);
        var fed53 = CalculateFederal(PayFrequency.Weekly, ThursdayAnchor53);

        Assert.True(fed52 > 0m);
        Assert.True(fed53 > 0m);
        Assert.NotEqual(fed52, fed53);
    }

    [Fact]
    public void Federal_Biweekly_27PeriodAnchor_DiffersFrom26PeriodAnchor()
    {
        var fed26 = CalculateFederal(PayFrequency.Biweekly, BiweeklyAnchor26);
        var fed27 = CalculateFederal(PayFrequency.Biweekly, BiweeklyAnchor27);

        Assert.True(fed26 > 0m);
        Assert.True(fed27 > 0m);
        Assert.NotEqual(fed26, fed27);
    }

    [Fact]
    public void Federal_NullPayDate_MatchesFixed52WeekResult()
    {
        // Backward compatibility: omitting the pay date behaves exactly like the old fixed table.
        var fedNull = CalculateFederal(PayFrequency.Weekly, payDate: null);
        var fed52 = CalculateFederal(PayFrequency.Weekly, FridayAnchor52);

        Assert.Equal(fed52, fedNull);
    }

    [Fact]
    public void Salary_AnnualBasis_Weekly_GrossDiffersBetween52And53WeekAnchors()
    {
        // A per-year salary is divided by the resolved period count, so the per-period gross
        // (and thus the whole paycheck) shifts when the year has 53 weeks.
        var gross52 = CalculateSalaryGross(FridayAnchor52);
        var gross53 = CalculateSalaryGross(ThursdayAnchor53);

        Assert.NotEqual(gross52, gross53);
    }

    [Fact]
    public void State_Virginia_Weekly_Withholding_RespectsResolvedPeriodCount()
    {
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        // Identical per-period wages; only the resolved pay-period count differs.
        var context52 = new CommonWithholdingContext(UsState.VA, 2_000m, PayFrequency.Weekly, 2026)
        {
            PayPeriodsPerYear = 52
        };
        var context53 = new CommonWithholdingContext(UsState.VA, 2_000m, PayFrequency.Weekly, 2026)
        {
            PayPeriodsPerYear = 53
        };

        var wh52 = calc.Calculate(context52, values).Withholding;
        var wh53 = calc.Calculate(context53, values).Withholding;

        Assert.True(wh52 > 0m);
        Assert.True(wh53 > 0m);
        Assert.NotEqual(wh52, wh53);
    }

    [Fact]
    public void State_Context_FallsBackToFixedCount_WhenPayPeriodsPerYearNotSet()
    {
        // A context built without setting PayPeriodsPerYear uses the fixed per-frequency count,
        // so an unset weekly context matches an explicitly-52 context.
        var calc = new VirginiaWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var unset = new CommonWithholdingContext(UsState.VA, 2_000m, PayFrequency.Weekly, 2026);
        var explicit52 = new CommonWithholdingContext(UsState.VA, 2_000m, PayFrequency.Weekly, 2026)
        {
            PayPeriodsPerYear = 52
        };

        Assert.Equal(52, unset.PayPeriodsPerYear);
        Assert.Equal(
            calc.Calculate(explicit52, values).Withholding,
            calc.Calculate(unset, values).Withholding);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static decimal CalculateFederal(PayFrequency frequency, DateOnly? payDate)
    {
        var payCalc = BuildPayCalculator();
        var input = new PaycheckInput
        {
            Frequency = frequency,
            PayDate = payDate,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            }
        };
        return payCalc.Calculate(input).FederalWithholding;
    }

    private static decimal CalculateSalaryGross(DateOnly payDate)
    {
        var payCalc = BuildPayCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Weekly,
            PayDate = payDate,
            PayType = PayType.Salary,
            SalaryAmount = 104_000m,
            SalaryBasis = SalaryBasis.PerYear,
            State = UsState.TX
        };
        return payCalc.Calculate(input).GrossPay;
    }

    private static PayCalculator BuildPayCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
