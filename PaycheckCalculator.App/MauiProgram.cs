using Microsoft.Extensions.Logging;
using PaycheckCalculator.App.Services;
using PaycheckCalculator.App.Services.Csv;
using PaycheckCalculator.App.Services.Pdf;
using PaycheckCalculator.App.Services.Printing;
using PaycheckCalculator.App.Services.Storage;
using PaycheckCalculator.App.Services.Sync;
using PaycheckCalculator.App.ViewModels;
using PaycheckCalculator.App.Views;
using PaycheckCalculator.Core.Budgeting;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Shared.Client;
using PaycheckCalculator.Shared.Entitlements;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── PaycheckCalculator.Core wiring (state/federal calculators, registries,
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
        builder.Services.AddSingleton<IJsonExportService, JsonExportService>();
        builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
        builder.Services.AddSingleton<PreferencesApiBaseAddressProvider>();
        builder.Services.AddSingleton<IApiBaseAddressProvider>(sp => sp.GetRequiredService<PreferencesApiBaseAddressProvider>());
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<PaycheckApiClient>();
        builder.Services.AddSingleton<PaycheckSyncService>();
        builder.Services.AddSingleton<ISyncCoordinator, SyncCoordinator>();

        // Entitlement provider — defaults to free tier until E2 billing is wired.
        builder.Services.AddSingleton<IEntitlementProvider, FreeEntitlementProvider>();

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
