using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Pay;

/// <summary>
/// Maps a <see cref="PayFrequency"/> to the number of pay periods in a year.
/// Used to convert an annual salary into a single period's gross pay.
/// </summary>
public static class PayPeriods
{
    /// <summary>Number of pay periods in a year for the given frequency.</summary>
    public static int PerYear(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        PayFrequency.Daily => 260,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
