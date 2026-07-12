using PaycheckCalculator.App.ViewModels;

namespace PaycheckCalculator.App.Views;

public partial class PaychecksPage : ContentPage
{
    public PaychecksPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
