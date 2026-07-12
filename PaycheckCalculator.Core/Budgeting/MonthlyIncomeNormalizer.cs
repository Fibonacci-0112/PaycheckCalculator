using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;

namespace PaycheckCalculator.Core.Budgeting;

/// <summary>
/// Converts a per-period net pay amount into a monthly equivalent by scaling with the number of
/// pay periods per year, then dividing by 12. Uses <see cref="PayPeriods.PerYear"/> so all
/// frequencies (including Weekly53, Biweekly27, etc.) are handled correctly — never x 4.
/// </summary>
public static class MonthlyIncomeNormalizer
{
    /// <summary>Returns the monthly equivalent of <paramref name="result"/>'s net pay.</summary>
    public static decimal ToMonthly(PaycheckResult result, PayFrequency frequency)
        => ToMonthly(result.NetPay, frequency);

    /// <summary>Returns the monthly equivalent of <paramref name="netPayPerPeriod"/>.</summary>
    public static decimal ToMonthly(decimal netPayPerPeriod, PayFrequency frequency)
        => Math.Round(netPayPerPeriod * PayPeriods.PerYear(frequency) / 12m, 2, MidpointRounding.AwayFromZero);
}
