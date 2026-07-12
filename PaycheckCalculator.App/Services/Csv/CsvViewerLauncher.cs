using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

#if ANDROID
using Android.Content;
// alias the AndroidX FileProvider to avoid ambiguity with Microsoft.Maui.Storage.FileProvider
using AndroidFileProvider = AndroidX.Core.Content.FileProvider;
#endif

#if WINDOWS
using System.Diagnostics;
#endif

namespace PaycheckCalculator.App.Services.Csv;

/// <inheritdoc />
public sealed class CsvViewerLauncher : ICsvViewerLauncher
{
    private const string CsvMimeType = "text/csv";

    /// <inheritdoc />
    public async Task OpenCsvAsync(string filePath)
    {
#if WINDOWS
        if (TryOpenWithDefaultAppWindows(filePath))
            return;
#elif ANDROID
        if (TryOpenWithDefaultAppAndroid(filePath))
            return;
#endif
        // Fallback: let the OS pick the default CSV handler.
        await Launcher.Default.OpenAsync(new OpenFileRequest("Paycheck Summary", new ReadOnlyFile(filePath)));
    }

#if WINDOWS
    private static bool TryOpenWithDefaultAppWindows(string filePath)
    {
        try
        {
            // UseShellExecute invokes the registered handler for the ".csv"
            // extension (Microsoft Excel when installed, otherwise the user's
            // chosen default), opening the file directly.
            using var process = Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            // No application is associated with ".csv".
            return false;
        }
    }
#endif

#if ANDROID
    private static bool TryOpenWithDefaultAppAndroid(string filePath)
    {
        try
        {
            var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            var authority = context.PackageName + ".fileProvider";
            var uri = AndroidFileProvider.GetUriForFile(context, authority, new Java.IO.File(filePath));

            // An implicit VIEW intent resolves to the device's default CSV
            // handler (or the system "Open with" chooser when none is set).
            using var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, CsvMimeType);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

            context.StartActivity(intent);
            return true;
        }
        catch (Exception)
        {
            // No app registered for CSV, or unable to handle the intent.
            return false;
        }
    }
#endif
}
