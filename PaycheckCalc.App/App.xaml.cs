using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly CalculatorViewModel _calculatorViewModel;

    public App(AppShell shell, CalculatorViewModel calculatorViewModel)
    {
        InitializeComponent();
        _shell = shell;
        _calculatorViewModel = calculatorViewModel;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell)
        {
            Width = 800,
            Height = 800,
        };

        // Load locally persisted paychecks (and trigger a sync if signed in) once at launch.
        // Window.Created fires on the UI thread, so InitializeAsync can safely touch bound collections.
        window.Created += (_, _) => _ = _calculatorViewModel.InitializeAsync();

        return window;
    }
}
