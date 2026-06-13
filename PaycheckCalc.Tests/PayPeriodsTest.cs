using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="PayPeriods.PerYear(PayFrequency, System.DateOnly?)"/>, which resolves the
/// actual number of pay periods in a calendar year against an anchor pay date.
/// <para>
/// Weekly and biweekly schedules can land an extra paycheck (53 / 27) depending on the anchor
/// date's weekday and whether the year is a leap year. Expected counts below are derived directly
/// from the calendar, not recomputed with the helper:
/// </para>
/// <list type="bullet">
///   <item>A 365-day year = 52×7 + 1, so the weekday of Jan 1 occurs 53 times; every other
///   weekday occurs 52. Jan 1 2026 is a Thursday.</item>
///   <item>A 366-day (leap) year = 52×7 + 2, so the weekdays of Jan 1 and Jan 2 each occur 53
///   times. Jan 1 2028 is a Saturday (so Sat and Sun anchors → 53).</item>
///   <item>Biweekly (interval 14): 365 = 26×14 + 1, so a year yields 27 paychecks only when the
///   anchor aligns with the year's first biweekly date (≡ Jan 1 mod 14), otherwise 26. A leap
///   year (366 = 26×14 + 2) yields 27 for two consecutive alignments.</item>
/// </list>
/// </summary>
public sealed class PayPeriodsTest
{
    // ── Weekly, non-leap year (2026, Jan 1 = Thursday) ──────────────

    [Fact]
    public void Weekly_ThursdayAnchor_NonLeapYear_Is53()
    {
        // 2026-01-01 is a Thursday — the weekday that occurs 53 times in 2026.
        Assert.Equal(53, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Weekly_NonThursdayAnchor_NonLeapYear_Is52()
    {
        // 2026-01-02 is a Friday — occurs only 52 times in 2026.
        Assert.Equal(52, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2026, 1, 2)));
    }

    [Fact]
    public void Weekly_CountIsWeekdayBased_NotDateBased()
    {
        // 2026-07-02 is also a Thursday, mid-year — still 53.
        Assert.Equal(53, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2026, 7, 2)));
    }

    [Fact]
    public void Weekly_LastDayOfYearAnchor_Is53()
    {
        // 2026-12-31 is a Thursday (boundary of the counting window).
        Assert.Equal(53, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2026, 12, 31)));
    }

    // ── Weekly, leap year (2028, Jan 1 = Saturday) ──────────────────

    [Fact]
    public void Weekly_SaturdayAnchor_LeapYear_Is53()
    {
        // 2028 is a leap year; Jan 1 is a Saturday → Saturdays occur 53 times.
        Assert.Equal(53, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2028, 1, 1)));
    }

    [Fact]
    public void Weekly_SundayAnchor_LeapYear_Is53()
    {
        // 2028-01-02 is a Sunday → the second weekday that occurs 53 times in a leap year.
        Assert.Equal(53, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2028, 1, 2)));
    }

    [Fact]
    public void Weekly_MondayAnchor_LeapYear_Is52()
    {
        // 2028-01-03 is a Monday → only 52 occurrences.
        Assert.Equal(52, PayPeriods.PerYear(PayFrequency.Weekly, new DateOnly(2028, 1, 3)));
    }

    // ── Biweekly ────────────────────────────────────────────────────

    [Fact]
    public void Biweekly_AlignedAnchor_NonLeapYear_Is27()
    {
        // 2026-01-01 is the first biweekly date of 2026 → 27 paychecks.
        Assert.Equal(27, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Biweekly_AlignedAnchor_FullPeriodLater_NonLeapYear_Is27()
    {
        // 2026-01-15 is 14 days after Jan 1 — same biweekly alignment → still 27.
        Assert.Equal(27, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Biweekly_MisalignedAnchor_NonLeapYear_Is26()
    {
        // 2026-01-08 is offset 7 days from the first biweekly date → 26 paychecks.
        Assert.Equal(26, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2026, 1, 8)));
    }

    [Fact]
    public void Biweekly_AlignedAnchor_LeapYear_Is27()
    {
        // 2028 leap year, Jan 1 aligned → 27.
        Assert.Equal(27, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2028, 1, 1)));
    }

    [Fact]
    public void Biweekly_SecondAlignment_LeapYear_Is27()
    {
        // In a leap year the day after the first alignment also yields 27 (366 = 26×14 + 2).
        Assert.Equal(27, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2028, 1, 2)));
    }

    [Fact]
    public void Biweekly_Misaligned_LeapYear_Is26()
    {
        Assert.Equal(26, PayPeriods.PerYear(PayFrequency.Biweekly, new DateOnly(2028, 1, 3)));
    }

    // ── Null pay date falls back to the fixed table ─────────────────

    [Fact]
    public void NullPayDate_Weekly_FallsBackTo52()
    {
        Assert.Equal(52, PayPeriods.PerYear(PayFrequency.Weekly, null));
    }

    [Fact]
    public void NullPayDate_Biweekly_FallsBackTo26()
    {
        Assert.Equal(26, PayPeriods.PerYear(PayFrequency.Biweekly, null));
    }

    // ── Non weekly/biweekly frequencies ignore the pay date ─────────

    [Theory]
    [InlineData(PayFrequency.Semimonthly, 24)]
    [InlineData(PayFrequency.Monthly, 12)]
    [InlineData(PayFrequency.Quarterly, 4)]
    [InlineData(PayFrequency.Semiannual, 2)]
    [InlineData(PayFrequency.Annual, 1)]
    [InlineData(PayFrequency.Daily, 260)]
    public void OtherFrequencies_WithPayDate_UseFixedCount(PayFrequency frequency, int expected)
    {
        // Even with a pay date supplied, only weekly/biweekly vary; all others stay fixed.
        Assert.Equal(expected, PayPeriods.PerYear(frequency, new DateOnly(2026, 1, 1)));
        Assert.Equal(expected, PayPeriods.PerYear(frequency, null));
    }
}
