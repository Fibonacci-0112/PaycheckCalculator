using PaycheckCalculator.App.Helpers;
using PaycheckCalculator.App.Models;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.App.Mappers;

/// <summary>
/// Maps a domain <see cref="HourlySalaryResult"/> to a <see cref="HourlySalaryCardModel"/>
/// presentation model. Purely a projection — no arithmetic happens here.
/// </summary>
public static class HourlySalaryCardMapper
{
    public static HourlySalaryCardModel Map(HourlySalaryResult result)
        => new()
        {
            Mode = result.Mode,
            HourlyRate = result.HourlyRate,
            AnnualSalary = result.AnnualSalary,
            HoursPerWeek = result.HoursPerWeek,
            WeeksPerYear = result.WeeksPerYear,
            HoursPerYear = result.HoursPerYear,
            PerPeriodPay = result.PerPeriodPay,
            FrequencyLabel = EnumDisplay.PayFrequency(result.Frequency.ToString()),
            WeeklyPay = result.WeeklyPay,
            BiweeklyPay = result.BiweeklyPay,
            SemimonthlyPay = result.SemimonthlyPay,
            MonthlyPay = result.MonthlyPay
        };
}
