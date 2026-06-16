using System;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="HourlySalaryCalculator"/> — the hourly ↔ salary converter
/// (roadmap D2). Expected values are taken directly from the rate × hours / ÷ pay-periods
/// arithmetic, not recomputed with production helpers.
/// </summary>
public sealed class HourlySalaryCalculatorTest
{
    private static HourlySalaryCalculator CreateCalculator() => new();

    // ── Hourly → salary ───────────────────────────────────────────

    [Fact]
    public void HourlyToSalary_StandardFullTimeYear_ProducesEquivalentAnnualAndPeriods()
    {
        // $25/hr × 40 hrs/wk × 52 wks = $52,000 / yr.
        //   Weekly       = 52000 / 52 = 1000.00
        //   Biweekly     = 52000 / 26 = 2000.00
        //   Semimonthly  = 52000 / 24 = 2166.666… → 2166.67
        //   Monthly      = 52000 / 12 = 4333.333… → 4333.33
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 25m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m,
            Frequency = PayFrequency.Biweekly
        };

        var result = calculator.Convert(input);

        Assert.Equal(PayConversionMode.HourlyToSalary, result.Mode);
        Assert.Equal(25.00m, result.HourlyRate);
        Assert.Equal(52000.00m, result.AnnualSalary);
        Assert.Equal(40m, result.HoursPerWeek);
        Assert.Equal(52m, result.WeeksPerYear);
        Assert.Equal(2080m, result.HoursPerYear);
        Assert.Equal(PayFrequency.Biweekly, result.Frequency);
        Assert.Equal(2000.00m, result.PerPeriodPay);
        Assert.Equal(1000.00m, result.WeeklyPay);
        Assert.Equal(2000.00m, result.BiweeklyPay);
        Assert.Equal(2166.67m, result.SemimonthlyPay);
        Assert.Equal(4333.33m, result.MonthlyPay);
    }

    [Fact]
    public void HourlyToSalary_UsesDefaultFullTimeHoursAndWeeks()
    {
        // Defaults: 40 hrs/wk, 52 wks. $30/hr → 30 × 2080 = $62,400.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput { HourlyRate = 30m };

        var result = calculator.Convert(input);

        Assert.Equal(40m, result.HoursPerWeek);
        Assert.Equal(52m, result.WeeksPerYear);
        Assert.Equal(62400.00m, result.AnnualSalary);
        Assert.Equal(30.00m, result.HourlyRate);
    }

    [Fact]
    public void HourlyToSalary_FewerPaidWeeks_LowersAnnual()
    {
        // Unpaid time off: 48 paid weeks. $20/hr × 40 × 48 = $38,400.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            HourlyRate = 20m,
            HoursPerWeek = 40m,
            WeeksPerYear = 48m
        };

        var result = calculator.Convert(input);

        Assert.Equal(1920m, result.HoursPerYear);
        Assert.Equal(38400.00m, result.AnnualSalary);
    }

    [Fact]
    public void HourlyToSalary_ReportsSelectedFrequencyPerPeriod()
    {
        // $45/hr × 2080 = $93,600. Monthly selected → 93600 / 12 = 7800.00.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            HourlyRate = 45m,
            Frequency = PayFrequency.Monthly
        };

        var result = calculator.Convert(input);

        Assert.Equal(93600.00m, result.AnnualSalary);
        Assert.Equal(7800.00m, result.PerPeriodPay);
        Assert.Equal(7800.00m, result.MonthlyPay);
    }

    [Fact]
    public void HourlyToSalary_OddFrequency_UsesPayPeriodsPerYear()
    {
        // Weekly53 has 53 periods. $52,000 / 53 = 981.132… → 981.13.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            HourlyRate = 25m,
            Frequency = PayFrequency.Weekly53
        };

        var result = calculator.Convert(input);

        Assert.Equal(52000.00m, result.AnnualSalary);
        Assert.Equal(981.13m, result.PerPeriodPay);
    }

    // ── Salary → hourly ("what's my real hourly?") ────────────────

    [Fact]
    public void SalaryToHourly_StandardFullTimeYear_RecoversHourlyRate()
    {
        // $52,000 / (40 × 52 = 2080 hrs) = $25.00/hr.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = 52000m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        var result = calculator.Convert(input);

        Assert.Equal(PayConversionMode.SalaryToHourly, result.Mode);
        Assert.Equal(25.00m, result.HourlyRate);
        Assert.Equal(52000.00m, result.AnnualSalary);
        Assert.Equal(2080m, result.HoursPerYear);
    }

    [Fact]
    public void SalaryToHourly_OvertimeHours_RevealsLowerRealHourly()
    {
        // A $52,000 salary actually worked at 50 hrs/wk:
        //   50 × 52 = 2600 hrs → 52000 / 2600 = $20.00/hr (vs. $25 at 40 hrs).
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = 52000m,
            HoursPerWeek = 50m,
            WeeksPerYear = 52m
        };

        var result = calculator.Convert(input);

        Assert.Equal(2600m, result.HoursPerYear);
        Assert.Equal(20.00m, result.HourlyRate);
    }

    [Fact]
    public void SalaryToHourly_NonTerminatingRate_RoundsToCent()
    {
        // $50,000 / 2080 = 24.0384… → 24.04.
        //   Weekly  = 50000 / 52 = 961.538… → 961.54
        //   Monthly = 50000 / 12 = 4166.666… → 4166.67
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = 50000m
        };

        var result = calculator.Convert(input);

        Assert.Equal(24.04m, result.HourlyRate);
        Assert.Equal(50000.00m, result.AnnualSalary);
        Assert.Equal(961.54m, result.WeeklyPay);
        Assert.Equal(4166.67m, result.MonthlyPay);
    }

    [Fact]
    public void SalaryToHourly_IgnoresHourlyRateInput()
    {
        // In salary mode a stray HourlyRate must not affect the result.
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = 52000m,
            HourlyRate = 999m
        };

        var result = calculator.Convert(input);

        Assert.Equal(25.00m, result.HourlyRate);
    }

    // ── Edge cases ────────────────────────────────────────────────

    [Fact]
    public void Convert_ZeroHourlyRate_ProducesZeroSalary()
    {
        var calculator = CreateCalculator();
        var result = calculator.Convert(new HourlySalaryInput { HourlyRate = 0m });

        Assert.Equal(0.00m, result.HourlyRate);
        Assert.Equal(0.00m, result.AnnualSalary);
        Assert.Equal(0.00m, result.PerPeriodPay);
    }

    [Fact]
    public void Convert_NullInput_Throws()
    {
        var calculator = CreateCalculator();
        Assert.Throws<ArgumentNullException>(() => calculator.Convert(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Convert_NonPositiveHoursPerWeek_Throws(int hoursPerWeek)
    {
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput { HourlyRate = 25m, HoursPerWeek = hoursPerWeek };

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Convert(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Convert_NonPositiveWeeksPerYear_Throws(int weeksPerYear)
    {
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput { HourlyRate = 25m, WeeksPerYear = weeksPerYear };

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Convert(input));
    }

    [Fact]
    public void Convert_NegativeHourlyRate_Throws()
    {
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput { HourlyRate = -1m };

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Convert(input));
    }

    [Fact]
    public void Convert_NegativeAnnualSalary_Throws()
    {
        var calculator = CreateCalculator();
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = -1m
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Convert(input));
    }
}
