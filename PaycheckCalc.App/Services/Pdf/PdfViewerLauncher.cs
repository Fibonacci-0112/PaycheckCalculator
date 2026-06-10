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

namespace PaycheckCalc.App.Services.Pdf;

/// <inheritdoc />
public sealed class PdfViewerLauncher : IPdfViewerLauncher
{
    private const string AdobeAndroidPackage = "com.adobe.reader";

    /// <inheritdoc />
    public async Task OpenPdfAsync(string filePath)
    {
#if ANDROID
        if (TryOpenInAdobeAndroid(filePath))
            return;
#elif WINDOWS
        if (TryOpenInAdobeWindows(filePath))
            return;
#endif
        // Fallback: let the OS pick the default PDF handler.
        await Launcher.Default.OpenAsync(new OpenFileRequest("Paycheck Summary", new ReadOnlyFile(filePath)));
    }

#if ANDROID
    private static bool TryOpenInAdobeAndroid(string filePath)
    {
        try
        {
            var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            var authority = context.PackageName + ".fileProvider";
            var uri = AndroidFileProvider.GetUriForFile(context, authority, new Java.IO.File(filePath));

            using var intent = new Intent(Intent.ActionView);
            intent.SetPackage(AdobeAndroidPackage);
            intent.SetDataAndType(uri, "application/pdf");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

            context.StartActivity(intent);
            return true;
        }
        catch (Exception)
        {
            // Adobe Reader not installed or unable to handle the intent.
            return false;
        }
    }
#endif

#if WINDOWS
    private static bool TryOpenInAdobeWindows(string filePath)
    {
        try
        {
            var exe = FindAdobeExecutable();
            if (exe is null)
                return false;

            Process.Start(new ProcessStartInfo(exe, $"\"{filePath}\"") { UseShellExecute = false });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? FindAdobeExecutable()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        // Probe common Acrobat (Acrobat.exe) and legacy Reader (AcroRd32.exe) locations.
        string[] candidates =
        {
            Path.Combine(programFiles, @"Adobe\Acrobat\Acrobat\Acrobat.exe"),
            Path.Combine(programFiles, @"Adobe\Acrobat DC\Acrobat\Acrobat.exe"),
            Path.Combine(programFilesX86, @"Adobe\Acrobat DC\Acrobat\Acrobat.exe"),
            Path.Combine(programFilesX86, @"Adobe\Acrobat Reader DC\Reader\AcroRd32.exe"),
            Path.Combine(programFiles, @"Adobe\Acrobat Reader DC\Reader\AcroRd32.exe"),
            Path.Combine(programFilesX86, @"Adobe\Reader\Reader\AcroRd32.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
#endif
}
