using PaycheckCalc.Core.Budgeting;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="SavingsGoal.Remaining"/> and <see cref="SavingsGoal.MonthlyContributionNeeded"/>.
/// Expected values are computed directly from the documented rule (Remaining / wholeMonthsRemaining,
/// rounded away-from-zero), never from the production helper.
/// </summary>
public sealed class SavingsGoalTest
{
    [Fact]
    public void Remaining_IsTargetMinusCurrent()
    {
        var goal = new SavingsGoal { Name = "Car", TargetAmount = 10_000m, CurrentAmount = 2_500m };
        Assert.Equal(7_500m, goal.Remaining);
    }

    [Fact]
    public void Remaining_NeverNegative_WhenOverfunded()
    {
        var goal = new SavingsGoal { Name = "Car", TargetAmount = 10_000m, CurrentAmount = 12_000m };
        Assert.Equal(0m, goal.Remaining);
    }

    [Fact]
    public void MonthlyContributionNeeded_SpreadsRemainingOverWholeMonths()
    {
        // $6,000 remaining over 6 months (Jun → Dec) = $1,000.00 / month
        var goal = new SavingsGoal
        {
            Name = "Vacation",
            TargetAmount = 6_000m,
            CurrentAmount = 0m,
            TargetDate = new DateOnly(2026, 12, 15)
        };
        Assert.Equal(1_000m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void MonthlyContributionNeeded_RoundsAwayFromZero()
    {
        // $10.01 remaining over 2 months = $5.005 → $5.01 (away-from-zero, not banker's $5.00)
        var goal = new SavingsGoal
        {
            Name = "Gadget",
            TargetAmount = 10.01m,
            CurrentAmount = 0m,
            TargetDate = new DateOnly(2026, 8, 1)
        };
        Assert.Equal(5.01m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void MonthlyContributionNeeded_DividesEvenly()
    {
        // $1,000 remaining over 3 months (Jun → Sep) = $333.33 (333.33... rounded to the cent)
        var goal = new SavingsGoal
        {
            Name = "Gadget",
            TargetAmount = 1_000m,
            CurrentAmount = 0m,
            TargetDate = new DateOnly(2026, 9, 1)
        };
        Assert.Equal(333.33m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void MonthlyContributionNeeded_AlreadyMet_ReturnsZero()
    {
        var goal = new SavingsGoal
        {
            Name = "Done",
            TargetAmount = 1_000m,
            CurrentAmount = 1_000m,
            TargetDate = new DateOnly(2026, 12, 1)
        };
        Assert.Equal(0m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void MonthlyContributionNeeded_OpenEnded_NoTargetDate_ReturnsZero()
    {
        var goal = new SavingsGoal { Name = "Rainy day", TargetAmount = 5_000m, CurrentAmount = 1_000m };
        Assert.Equal(0m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void MonthlyContributionNeeded_TargetThisMonthOrPast_RequiresFullRemaining()
    {
        // Target month equals current month → no whole months left → full remaining is due now.
        var goal = new SavingsGoal
        {
            Name = "Now",
            TargetAmount = 800m,
            CurrentAmount = 300m,
            TargetDate = new DateOnly(2026, 6, 30)
        };
        Assert.Equal(500m, goal.MonthlyContributionNeeded(new DateOnly(2026, 6, 1)));
    }
}
