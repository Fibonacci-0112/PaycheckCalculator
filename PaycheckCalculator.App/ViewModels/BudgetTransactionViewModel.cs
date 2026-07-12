using CommunityToolkit.Mvvm.ComponentModel;

namespace PaycheckCalculator.App.ViewModels;

public partial class BudgetTransactionViewModel : ObservableObject
{
    [ObservableProperty] public partial Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] public partial string CategoryName { get; set; } = "";
    [ObservableProperty] public partial decimal Amount { get; set; }
    [ObservableProperty] public partial DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] public partial string Description { get; set; } = "";

    public string DisplayDate => Date.ToString("MMM d");
}
