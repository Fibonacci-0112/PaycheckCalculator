using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Validation;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="PaycheckInputValidator.ValidateHourlySalary"/> — the front-end guard that
/// turns the converter's <see cref="System.ArgumentOutOfRangeException"/> preconditions into
/// user-facing messages before <see cref="Core.Pay.HourlySalaryCalculator"/> is ever called.
/// </summary>
public sealed class HourlySalaryValidationTest
{
    [Fact]
    public void ValidHourlyToSalaryInput_ProducesNoErrors()
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 25m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        Assert.Empty(PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void ValidSalaryToHourlyInput_ProducesNoErrors()
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = 52000m,
            HoursPerWeek = 50m,
            WeeksPerYear = 50m
        };

        Assert.Empty(PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void ZeroHourlyRate_IsAllowed()
    {
        // A $0 rate is a degenerate but legal conversion (it yields a $0 salary); only a
        // negative rate is rejected.
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 0m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        Assert.Empty(PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public void NonPositiveHoursPerWeek_IsRejected(int hoursPerWeek)
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 25m,
            HoursPerWeek = hoursPerWeek,
            WeeksPerYear = 52m
        };

        Assert.Contains("Hours per week must be greater than zero.",
            PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void NonPositiveWeeksPerYear_IsRejected(int weeksPerYear)
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 25m,
            HoursPerWeek = 40m,
            WeeksPerYear = weeksPerYear
        };

        Assert.Contains("Paid weeks per year must be greater than zero.",
            PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void NegativeHourlyRate_IsRejectedInHourlyToSalaryMode()
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = -1m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        Assert.Contains("Hourly rate cannot be negative.",
            PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void NegativeAnnualSalary_IsRejectedInSalaryToHourlyMode()
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = -1m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        Assert.Contains("Annual salary cannot be negative.",
            PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void NegativeSalary_IsIgnoredInHourlyToSalaryMode()
    {
        // The unused side of the conversion is never read by the calculator, so it must not
        // block a valid conversion in the other direction.
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = 25m,
            AnnualSalary = -999m,
            HoursPerWeek = 40m,
            WeeksPerYear = 52m
        };

        Assert.Empty(PaycheckInputValidator.ValidateHourlySalary(input));
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var input = new HourlySalaryInput
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = -5m,
            HoursPerWeek = 0m,
            WeeksPerYear = 0m
        };

        var errors = PaycheckInputValidator.ValidateHourlySalary(input);

        Assert.Equal(3, errors.Count);
    }
}
