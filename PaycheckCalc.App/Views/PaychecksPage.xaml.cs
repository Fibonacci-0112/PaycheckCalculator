using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class PaychecksPage : ContentPage
{
    public PaychecksPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
