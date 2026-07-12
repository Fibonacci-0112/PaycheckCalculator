using PaycheckCalculator.Core.Budgeting;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="RecurrencePeriods"/> and <see cref="RecurringBill.MonthlyEquivalent"/>.
/// Expected values are computed directly from the documented rule (amount × PerYear / 12, rounded
/// away-from-zero), never from the production helper.
/// </summary>
public sealed class RecurringBillTest
{
    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, 52)]
    [InlineData(RecurrenceFrequency.Biweekly, 26)]
    [InlineData(RecurrenceFrequency.Semimonthly, 24)]
    [InlineData(RecurrenceFrequency.Monthly, 12)]
    [InlineData(RecurrenceFrequency.Quarterly, 4)]
    [InlineData(RecurrenceFrequency.Semiannual, 2)]
    [InlineData(RecurrenceFrequency.Annual, 1)]
    public void PerYear_MatchesFrequency(RecurrenceFrequency frequency, int expected)
        => Assert.Equal(expected, RecurrencePeriods.PerYear(frequency));

    [Fact]
    public void MonthlyEquivalent_Monthly_ReturnsAmount()
    {
        // $1,200 × 12 / 12 = $1,200.00
        Assert.Equal(1_200m, RecurrencePeriods.MonthlyEquivalent(1_200m, RecurrenceFrequency.Monthly));
    }

    [Fact]
    public void MonthlyEquivalent_Annual_DividesAcrossTwelveMonths()
    {
        // $1,200 × 1 / 12 = $100.00
        Assert.Equal(100m, RecurrencePeriods.MonthlyEquivalent(1_200m, RecurrenceFrequency.Annual));
    }

    [Fact]
    public void MonthlyEquivalent_Quarterly_DividesAcrossTwelveMonths()
    {
        // $300 × 4 / 12 = $100.00
        Assert.Equal(100m, RecurrencePeriods.MonthlyEquivalent(300m, RecurrenceFrequency.Quarterly));
    }

    [Fact]
    public void MonthlyEquivalent_Weekly_UsesFiftyTwoNotFour()
    {
        // $100 × 52 / 12 = $433.33 (rounded away-from-zero), proving it is not "× 4" = $400
        Assert.Equal(433.33m, RecurrencePeriods.MonthlyEquivalent(100m, RecurrenceFrequency.Weekly));
    }

    [Fact]
    public void MonthlyEquivalent_Biweekly_UsesTwentySix()
    {
        // $200 × 26 / 12 = $433.33 (rounded away-from-zero)
        Assert.Equal(433.33m, RecurrencePeriods.MonthlyEquivalent(200m, RecurrenceFrequency.Biweekly));
    }

    [Fact]
    public void RecurringBill_MonthlyEquivalent_DelegatesToRecurrencePeriods()
    {
        // $60 weekly × 52 / 12 = $260.00
        var bill = new RecurringBill
        {
            Name = "Groceries",
            CategoryName = "Food",
            Amount = 60m,
            Frequency = RecurrenceFrequency.Weekly
        };
        Assert.Equal(260m, bill.MonthlyEquivalent);
    }

    [Fact]
    public void RecurringBill_DefaultFrequency_IsMonthly()
    {
        var bill = new RecurringBill { Name = "Rent", CategoryName = "Housing", Amount = 1_500m };
        Assert.Equal(RecurrenceFrequency.Monthly, bill.Frequency);
        Assert.Equal(1_500m, bill.MonthlyEquivalent);
    }
}
