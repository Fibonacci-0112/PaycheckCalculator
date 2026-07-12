using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.Core.Budgeting;

namespace PaycheckCalculator.App.ViewModels;

public partial class SavingsGoalViewModel : ObservableObject
{
    [ObservableProperty] public partial Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial decimal TargetAmount { get; set; }
    [ObservableProperty] public partial decimal CurrentAmount { get; set; }
    [ObservableProperty] public partial DateOnly? TargetDate { get; set; }

    public decimal Remaining => Math.Max(0m, TargetAmount - CurrentAmount);

    public decimal MonthlyContributionNeeded => ToDomain().MonthlyContributionNeeded(DateOnly.FromDateTime(DateTime.Today));

    public string TargetDateLabel => TargetDate?.ToString("MMM yyyy") ?? "No target date";

    partial void OnTargetAmountChanged(decimal value) => NotifyComputed();
    partial void OnCurrentAmountChanged(decimal value) => NotifyComputed();
    partial void OnTargetDateChanged(DateOnly? value) => NotifyComputed();

    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(MonthlyContributionNeeded));
        OnPropertyChanged(nameof(TargetDateLabel));
    }

    public SavingsGoal ToDomain() => new()
    {
        Id = Id,
        Name = Name,
        TargetAmount = TargetAmount,
        CurrentAmount = CurrentAmount,
        TargetDate = TargetDate
    };
}
