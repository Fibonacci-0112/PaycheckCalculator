using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Validation;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="PaycheckInputValidator.ValidateHourlySalary"/>, the shared pre-flight
/// validation both front-ends run before invoking <see cref="Core.Pay.HourlySalaryCalculator"/>.
/// It exists so the UIs surface friendly messages instead of letting the calculator throw.
/// </summary>
public sealed class HourlySalaryValidationTest
{
    private static HourlySalaryInput HourlyToSalary(
        decimal hourlyRate = 25m,
        decimal hoursPerWeek = 40m,
        decimal weeksPerYear = 52m,
        decimal annualSalary = 0m) => new()
        {
            Mode = PayConversionMode.HourlyToSalary,
            HourlyRate = hourlyRate,
            AnnualSalary = annualSalary,
            HoursPerWeek = hoursPerWeek,
            WeeksPerYear = weeksPerYear,
            Frequency = PayFrequency.Biweekly
        };

    private static HourlySalaryInput SalaryToHourly(
        decimal annualSalary = 52000m,
        decimal hoursPerWeek = 40m,
        decimal weeksPerYear = 52m,
        decimal hourlyRate = 0m) => new()
        {
            Mode = PayConversionMode.SalaryToHourly,
            AnnualSalary = annualSalary,
            HourlyRate = hourlyRate,
            HoursPerWeek = hoursPerWeek,
            WeeksPerYear = weeksPerYear,
            Frequency = PayFrequency.Biweekly
        };

    [Fact]
    public void ValidHourlyToSalaryInput_ProducesNoErrors()
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary());

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidSalaryToHourlyInput_ProducesNoErrors()
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(SalaryToHourly());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveHoursPerWeek_IsRejected(int hoursPerWeek)
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(hoursPerWeek: hoursPerWeek));

        Assert.Contains(errors, e => e.Contains("Hours per week must be greater than zero."));
    }

    [Fact]
    public void HoursPerWeekAtTheUpperBound_IsAccepted()
    {
        // 7 days × 24 hours = 168 is the documented maximum and must not be rejected.
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(hoursPerWeek: 168m));

        Assert.Empty(errors);
    }

    [Fact]
    public void HoursPerWeekAboveTheUpperBound_IsRejected()
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(hoursPerWeek: 168.01m));

        Assert.Contains(errors, e => e.Contains("Hours per week cannot exceed 168"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWeeksPerYear_IsRejected(int weeksPerYear)
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(weeksPerYear: weeksPerYear));

        Assert.Contains(errors, e => e.Contains("Paid weeks per year must be greater than zero."));
    }

    [Fact]
    public void WeeksPerYearAtTheUpperBound_IsAccepted()
    {
        // A 53-payday year is legitimate for weekly payrolls, so 53 must be accepted.
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(weeksPerYear: 53m));

        Assert.Empty(errors);
    }

    [Fact]
    public void WeeksPerYearAboveTheUpperBound_IsRejected()
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(weeksPerYear: 53.01m));

        Assert.Contains(errors, e => e.Contains("Paid weeks per year cannot exceed 53"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveHourlyRate_IsRejectedInHourlyToSalaryDirection(int hourlyRate)
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(hourlyRate: hourlyRate));

        Assert.Contains(errors, e => e.Contains("Hourly rate must be greater than zero."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveAnnualSalary_IsRejectedInSalaryToHourlyDirection(int annualSalary)
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(SalaryToHourly(annualSalary: annualSalary));

        Assert.Contains(errors, e => e.Contains("Annual salary must be greater than zero."));
    }

    [Fact]
    public void BlankAnnualSalary_IsIgnoredInHourlyToSalaryDirection()
    {
        // Hourly → salary only reads HourlyRate, so a blank annual salary must not error.
        var errors = PaycheckInputValidator.ValidateHourlySalary(HourlyToSalary(annualSalary: 0m));

        Assert.Empty(errors);
    }

    [Fact]
    public void BlankHourlyRate_IsIgnoredInSalaryToHourlyDirection()
    {
        var errors = PaycheckInputValidator.ValidateHourlySalary(SalaryToHourly(hourlyRate: 0m));

        Assert.Empty(errors);
    }

    [Fact]
    public void EveryInvalidField_IsReportedTogether()
    {
        var input = HourlyToSalary(hourlyRate: 0m, hoursPerWeek: 0m, weeksPerYear: 0m);

        var errors = PaycheckInputValidator.ValidateHourlySalary(input);

        Assert.Equal(3, errors.Count);
    }
}
