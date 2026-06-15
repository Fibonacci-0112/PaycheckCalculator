namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// A savings target with the amount saved so far and an optional target date. The <see cref="Id"/> is a
/// stable GUID used as the sync key. <see cref="MonthlyContributionNeeded"/> spreads the remaining
/// amount across the whole months left until the target date.
/// </summary>
public sealed class SavingsGoal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }

    /// <summary>Optional date by which the goal should be met. When null the goal is open-ended.</summary>
    public DateOnly? TargetDate { get; init; }

    /// <summary>Amount still to be saved (never negative).</summary>
    public decimal Remaining => Math.Max(0m, TargetAmount - CurrentAmount);

    /// <summary>
    /// Monthly contribution required to reach the goal by <see cref="TargetDate"/>:
    /// <c>Remaining / wholeMonthsRemaining</c>, rounded to the cent (away-from-zero). Returns 0 when the
    /// goal is already met or open-ended (no target date). When the target month is the current month or
    /// in the past, the full remaining amount is required this month.
    /// </summary>
    public decimal MonthlyContributionNeeded(DateOnly today)
    {
        if (Remaining <= 0m) return 0m;
        if (TargetDate is not { } target) return 0m;

        var monthsRemaining = (target.Year - today.Year) * 12 + (target.Month - today.Month);
        if (monthsRemaining <= 0) return Remaining;

        return Math.Round(Remaining / monthsRemaining, 2, MidpointRounding.AwayFromZero);
    }
}
