using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Pay;

/// <summary>
/// Converts between an hourly rate and a salary, and breaks the equivalent earnings out
/// across pay frequencies. This is a pure rate conversion — no taxes, deductions, or
/// W-4 logic — and is the basis for the "what's my real hourly?" tool: divide a salary
/// by the hours actually worked to reveal the true hourly rate.
///
/// Annual salary is the pivot. Hourly → salary multiplies the rate by the hours worked
/// per year (<see cref="HourlySalaryInput.HoursPerWeek"/> × <see cref="HourlySalaryInput.WeeksPerYear"/>);
/// salary → hourly divides by the same. Per-frequency amounts come from the unrounded
/// annual divided by <see cref="PayPeriods.PerYear"/>, so the weekly/biweekly/etc.
/// period counts match the rest of the engine.
/// </summary>
public sealed class HourlySalaryCalculator
{
    /// <summary>
    /// Performs the conversion described by <paramref name="input"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Hours per week or weeks per year is not positive, or the source rate/salary is negative.
    /// </exception>
    public HourlySalaryResult Convert(HourlySalaryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.HoursPerWeek <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(input), input.HoursPerWeek, "Hours per week must be greater than 0.");
        if (input.WeeksPerYear <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(input), input.WeeksPerYear, "Weeks per year must be greater than 0.");

        var hoursPerYear = input.HoursPerWeek * input.WeeksPerYear;

        decimal hourly;
        decimal annual;
        if (input.Mode == PayConversionMode.HourlyToSalary)
        {
            if (input.HourlyRate < 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(input), input.HourlyRate, "Hourly rate cannot be negative.");
            hourly = input.HourlyRate;
            annual = hourly * hoursPerYear;
        }
        else
        {
            if (input.AnnualSalary < 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(input), input.AnnualSalary, "Annual salary cannot be negative.");
            annual = input.AnnualSalary;
            hourly = annual / hoursPerYear;
        }

        return new HourlySalaryResult
        {
            Mode = input.Mode,
            HourlyRate = RoundMoney(hourly),
            AnnualSalary = RoundMoney(annual),
            HoursPerWeek = input.HoursPerWeek,
            WeeksPerYear = input.WeeksPerYear,
            HoursPerYear = hoursPerYear,
            Frequency = input.Frequency,
            PerPeriodPay = PerPeriod(annual, input.Frequency),
            WeeklyPay = PerPeriod(annual, PayFrequency.Weekly),
            BiweeklyPay = PerPeriod(annual, PayFrequency.Biweekly),
            SemimonthlyPay = PerPeriod(annual, PayFrequency.Semimonthly),
            MonthlyPay = PerPeriod(annual, PayFrequency.Monthly)
        };
    }

    private static decimal PerPeriod(decimal annual, PayFrequency frequency) =>
        RoundMoney(annual / PayPeriods.PerYear(frequency));

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
