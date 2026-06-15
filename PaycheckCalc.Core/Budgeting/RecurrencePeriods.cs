namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// Converts a recurring amount into its monthly equivalent by scaling with the number of occurrences
/// per year, then dividing by 12 — the bill-side analogue of <see cref="MonthlyIncomeNormalizer"/>.
/// Mirrors <c>PayPeriods.PerYear</c> so frequencies are never approximated with "× 4".
/// </summary>
public static class RecurrencePeriods
{
    /// <summary>Number of times a bill recurs in a calendar year for the given frequency.</summary>
    public static int PerYear(RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => 52,
        RecurrenceFrequency.Biweekly => 26,
        RecurrenceFrequency.Semimonthly => 24,
        RecurrenceFrequency.Monthly => 12,
        RecurrenceFrequency.Quarterly => 4,
        RecurrenceFrequency.Semiannual => 2,
        RecurrenceFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported recurrence frequency")
    };

    /// <summary>
    /// Monthly equivalent of <paramref name="amount"/> charged at <paramref name="frequency"/>:
    /// <c>amount × PerYear(frequency) / 12</c>, rounded to the cent (away-from-zero).
    /// </summary>
    public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency)
        => Math.Round(amount * PerYear(frequency) / 12m, 2, MidpointRounding.AwayFromZero);
}
