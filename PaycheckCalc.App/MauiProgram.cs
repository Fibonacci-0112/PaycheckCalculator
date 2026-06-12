using Microsoft.Extensions.Logging;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Services.Pdf;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.DependencyInjection;

namespace PaycheckCalc.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── PaycheckCalc.Core wiring (state/federal calculators, registries,
        //    schema provider, tax JSON tables). MAUI reads the JSON from the app
        //    package via FileSystem.OpenAppPackageFileAsync.
        builder.Services.AddPaycheckCalcCore(new MauiAppPackageTaxDataReader());

        // PDF export of paycheck results + chart, opened in Adobe Reader/Acrobat.
        builder.Services.AddSingleton<IPdfViewerLauncher, PdfViewerLauncher>();
        builder.Services.AddSingleton<IPdfExportService, PdfExportService>();

        builder.Services.AddSingleton<CalculatorViewModel>();
        builder.Services.AddSingleton<InputsPage>();
        builder.Services.AddSingleton<ResultsPage>();
        builder.Services.AddSingleton<PaychecksPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
