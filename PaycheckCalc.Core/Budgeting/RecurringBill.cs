namespace PaycheckCalc.Core.Budgeting;

/// <summary>
/// A recurring expense (rent, utilities, subscriptions) assigned to a budget category. The
/// <see cref="Id"/> is a stable GUID used as the sync key. <see cref="MonthlyEquivalent"/> normalizes
/// the charge to a monthly figure so bills of any cadence can be summed against a monthly budget.
/// </summary>
public sealed class RecurringBill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public decimal Amount { get; init; }
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;

    /// <summary>Optional day of the month (1–31) the bill is due; informational, used for UI sorting/labels.</summary>
    public int? DueDayOfMonth { get; init; }

    /// <summary>The bill's monthly-equivalent cost (see <see cref="RecurrencePeriods.MonthlyEquivalent"/>).</summary>
    public decimal MonthlyEquivalent => RecurrencePeriods.MonthlyEquivalent(Amount, Frequency);
}
