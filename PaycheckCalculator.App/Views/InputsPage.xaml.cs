using PaycheckCalculator.App.ViewModels;

namespace PaycheckCalculator.App.Views;

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
            tab.BackgroundColor = Colors.Transparent;
            tab.TextColor = Token("Muted", Colors.Gray);
            tab.FontAttributes = FontAttributes.None;
        }

        // Activate selected tab
        tapped.BackgroundColor = Token("Primary", Colors.RoyalBlue);
        tapped.TextColor = Token("OnPrimary", Colors.White);
        tapped.FontAttributes = FontAttributes.Bold;

        // Toggle content visibility
        for (var i = 0; i < _contentPanels.Length; i++)
            _contentPanels[i].IsVisible = i == index;

        _activeTab = tapped;
    }

    /// <summary>
    /// Resolves a colour from Resources/Styles/Colors.xaml so the selected-tab styling
    /// stays in step with the XAML rather than duplicating hex literals here.
    /// </summary>
    private static Color Token(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
}
