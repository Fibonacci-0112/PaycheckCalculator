using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace PaycheckCalculator.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => PaycheckCalculator.App.MauiProgram.CreateMauiApp();
}
