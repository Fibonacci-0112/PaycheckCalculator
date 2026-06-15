using Microsoft.Extensions.Logging;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Services.Csv;
using PaycheckCalc.App.Services.Pdf;
using PaycheckCalc.App.Services.Printing;
using PaycheckCalc.App.Services.Storage;
using PaycheckCalc.App.Services.Sync;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.Budgeting;
using PaycheckCalc.Shared.Budgeting;
using PaycheckCalc.Core.DependencyInjection;
using PaycheckCalc.Shared.Client;
using PaycheckCalc.Shared.Sync;

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

        // CSV export of paycheck results, opened in the default CSV app (Excel).
        builder.Services.AddSingleton<ICsvViewerLauncher, CsvViewerLauncher>();
        builder.Services.AddSingleton<ICsvExportService, CsvExportService>();

        // Direct printing of paycheck results via the platform print system.
        builder.Services.AddSingleton<IPrintLauncher, PrintLauncher>();
        builder.Services.AddSingleton<IPrintService, PrintService>();

        // Local persistence (saved paychecks on device) + optional account sync.
        builder.Services.AddSingleton<ISavedPaycheckStore, JsonFilePaycheckStore>();
        builder.Services.AddSingleton<IBudgetStore, JsonFileBudgetStore>();
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<PreferencesApiBaseAddressProvider>();
        builder.Services.AddSingleton<IApiBaseAddressProvider>(sp => sp.GetRequiredService<PreferencesApiBaseAddressProvider>());
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<PaycheckApiClient>();
        builder.Services.AddSingleton<PaycheckSyncService>();
        builder.Services.AddSingleton<ISyncCoordinator, SyncCoordinator>();

        builder.Services.AddSingleton<CalculatorViewModel>();
        builder.Services.AddSingleton<AccountViewModel>();
        builder.Services.AddSingleton<BudgetViewModel>();
        builder.Services.AddSingleton<InputsPage>();
        builder.Services.AddSingleton<ResultsPage>();
        builder.Services.AddSingleton<PaychecksPage>();
        builder.Services.AddSingleton<BudgetPage>();
        builder.Services.AddSingleton<AccountPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
