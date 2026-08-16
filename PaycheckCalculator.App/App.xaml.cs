using PaycheckCalculator.App.ViewModels;

namespace PaycheckCalculator.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Only the service provider is injected: constructor arguments are resolved
    // before InitializeComponent() runs, so anything whose XAML uses
    // StaticResource (AppShell, pages) must be created *after* the application
    // resource dictionaries have been merged.
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = _services.GetRequiredService<AppShell>();
        var calculatorViewModel = _services.GetRequiredService<CalculatorViewModel>();

        var window = new Window(shell)
        {
            Width = 800,
            Height = 800,
        };

        // Load locally persisted paychecks (and trigger a sync if signed in) once at launch.
        // Window.Created fires on the UI thread, so InitializeAsync can safely touch bound collections.
        window.Created += (_, _) => _ = calculatorViewModel.InitializeAsync();

        return window;
    }
}
