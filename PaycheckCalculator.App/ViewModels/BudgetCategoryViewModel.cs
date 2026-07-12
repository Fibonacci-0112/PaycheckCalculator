using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.Core.Budgeting;

namespace PaycheckCalculator.App.ViewModels;

public partial class BudgetCategoryViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial BudgetType BudgetType { get; set; }
    [ObservableProperty] public partial decimal Amount { get; set; }
    [ObservableProperty] public partial BudgetAmountType AmountType { get; set; } = BudgetAmountType.Dollar;
    [ObservableProperty] public partial decimal Budgeted { get; set; }
    [ObservableProperty] public partial decimal Spent { get; set; }
    [ObservableProperty] public partial decimal ProjectedMonthEnd { get; set; }
    [ObservableProperty] public partial decimal Recurring { get; set; }

    public decimal Remaining => Budgeted - Spent;

    partial void OnBudgetedChanged(decimal value) => OnPropertyChanged(nameof(Remaining));
    partial void OnSpentChanged(decimal value)    => OnPropertyChanged(nameof(Remaining));

    public string BudgetTypeLabel => BudgetType.ToString();
}
