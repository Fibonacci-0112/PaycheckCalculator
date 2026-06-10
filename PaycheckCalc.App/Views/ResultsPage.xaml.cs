using PaycheckCalc.App.Controls;
using PaycheckCalc.App.ViewModels;
using System.ComponentModel;

namespace PaycheckCalc.App.Views;

public partial class ResultsPage : ContentPage
{
    private readonly CalculatorViewModel _vm;
    private readonly DoughnutChartDrawable _chartDrawable = new();

    public ResultsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

        DoughnutChart.Drawable = _chartDrawable;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateChart();
    }

    protected override void OnDisappearing()
    {
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorViewModel.ResultCard))
            UpdateChart();
    }

    private void UpdateChart()
    {
        _chartDrawable.Result = _vm.ResultCard;
        DoughnutChart.Invalidate();
    }
}
