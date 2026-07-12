using PaycheckCalculator.App.ViewModels;

namespace PaycheckCalculator.App.Views;

public partial class BudgetPage : ContentPage
{
    private readonly BudgetViewModel _vm;

    public BudgetPage(BudgetViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
