using PaycheckCalculator.App.ViewModels;

namespace PaycheckCalculator.App.Views;

public partial class AccountPage : ContentPage
{
    private readonly AccountViewModel _vm;

    public AccountPage(AccountViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
