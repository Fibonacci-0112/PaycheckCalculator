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

    /// <summary>
    /// Number of pay periods in a year, resolved against an anchor pay date.
    /// <para>
    /// For <see cref="PayFrequency.Weekly"/> and <see cref="PayFrequency.Biweekly"/> the actual
    /// count depends on the anchor date and leap years: a 365-day year (= 52×7 + 1) gives one
    /// weekday 53 occurrences, and a 366-day year gives two, so weekly resolves to 52 or 53 and
    /// biweekly to 26 or 27. Every other frequency — and a null <paramref name="payDate"/> — uses
    /// the fixed <see cref="PerYear(PayFrequency)"/> value.
    /// </para>
    /// <para>
    /// Counts the pay dates that fall within the anchor's own calendar year, i.e. dates in
    /// [Jan 1, Dec 31] of <c>payDate.Year</c> congruent to the anchor modulo the pay interval.
    /// </para>
    /// </summary>
    public static int PerYear(PayFrequency frequency, DateOnly? payDate)
    {
        if (payDate is not { } date)
            return PerYear(frequency);

        int interval = frequency switch
        {
            PayFrequency.Weekly => 7,
            PayFrequency.Biweekly => 14,
            _ => 0
        };
        if (interval == 0)
            return PerYear(frequency);

        // DateOnly.DayNumber counts days since 0001-01-01, so modular arithmetic on it is exact
        // and leap-year-safe (leap days shift every later DayNumber automatically).
        int anchorNum = date.DayNumber;
        int startNum = new DateOnly(date.Year, 1, 1).DayNumber;
        int endNum = new DateOnly(date.Year, 12, 31).DayNumber;

        // First pay date on or after Jan 1 that is congruent to the anchor modulo the interval.
        int offset = ((anchorNum - startNum) % interval + interval) % interval;
        int firstNum = startNum + offset;

        return (endNum - firstNum) / interval + 1;
    }
}
