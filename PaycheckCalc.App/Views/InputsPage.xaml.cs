using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class InputsPage : ContentPage
{
    private Button _activeTab;
    private readonly ScrollView[] _contentPanels;
    private readonly Button[] _tabButtons;

    public InputsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _tabButtons = [TabPayHours, TabFederal, TabState, TabDeductions];
        _contentPanels = [PayHoursContent, FederalContent, StateContent, DeductionsContent];
        _activeTab = TabPayHours;
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button tapped || tapped == _activeTab)
            return;

        var index = Array.IndexOf(_tabButtons, tapped);
        if (index < 0)
            return;

        // Reset all tabs to inactive style
        foreach (var tab in _tabButtons)
        {
            tab.BackgroundColor = Color.FromArgb("#ECECEC");
            tab.TextColor = Color.FromArgb("#777777");
            tab.FontAttributes = FontAttributes.None;
        }

        // Activate selected tab
        tapped.BackgroundColor = Color.FromArgb("#333333");
        tapped.TextColor = Colors.White;
        tapped.FontAttributes = FontAttributes.Bold;

        // Toggle content visibility
        for (var i = 0; i < _contentPanels.Length; i++)
            _contentPanels[i].IsVisible = i == index;

        _activeTab = tapped;
    }
}
