using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.Core.Budgeting;

namespace PaycheckCalculator.App.ViewModels;

public partial class RecurringBillViewModel : ObservableObject
{
    [ObservableProperty] public partial Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string CategoryName { get; set; } = "";
    [ObservableProperty] public partial decimal Amount { get; set; }
    [ObservableProperty] public partial RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    [ObservableProperty] public partial int? DueDayOfMonth { get; set; }

    public decimal MonthlyEquivalent => RecurrencePeriods.MonthlyEquivalent(Amount, Frequency);

    partial void OnAmountChanged(decimal value) => OnPropertyChanged(nameof(MonthlyEquivalent));
    partial void OnFrequencyChanged(RecurrenceFrequency value) => OnPropertyChanged(nameof(MonthlyEquivalent));

    public string FrequencyLabel => Frequency.ToString();
}
