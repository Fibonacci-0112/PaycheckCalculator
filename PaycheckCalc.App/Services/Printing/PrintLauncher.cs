using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

#if ANDROID
using Android.Content;
using Android.OS;
using Android.Print;
using Java.IO;
#endif

#if WINDOWS
using System.Diagnostics;
#endif

namespace PaycheckCalc.App.Services.Printing;

/// <inheritdoc />
public sealed class PrintLauncher : IPrintLauncher
{
    /// <inheritdoc />
    public async Task PrintAsync(string filePath, string jobName)
    {
#if ANDROID
        // The Android print dialog must be started from an Activity on the UI thread.
        await MainThread.InvokeOnMainThreadAsync(() => PrintAndroid(filePath, jobName));
#elif WINDOWS
        PrintWindows(filePath);
        await Task.CompletedTask;
#else
        // No native print pipeline on this platform: open the document so the
        // user can print from the system viewer.
        await Launcher.Default.OpenAsync(new OpenFileRequest(jobName, new ReadOnlyFile(filePath)));
#endif
    }

#if ANDROID
    private static void PrintAndroid(string filePath, string jobName)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No active activity is available to start printing.");

        var printManager = (PrintManager?)activity.GetSystemService(Context.PrintService)
            ?? throw new InvalidOperationException("Printing is not supported on this device.");

        var adapter = new PdfFilePrintDocumentAdapter(filePath, jobName);
        printManager.Print(jobName, adapter, null);
    }
#endif

#if WINDOWS
    private static void PrintWindows(string filePath)
    {
        // Invoke the "print" verb on the registered PDF handler (Edge, Adobe, etc.).
        using var process = Process.Start(new ProcessStartInfo(filePath)
        {
            Verb = "print",
            UseShellExecute = true,
        });
    }
#endif
}

#if ANDROID
/// <summary>
/// Streams an already-rendered PDF file to the Android print framework. The
/// document is produced up-front (by <see cref="PrintService"/>), so layout is a
/// no-op and writing simply copies the file bytes to the supplied descriptor.
/// </summary>
internal sealed class PdfFilePrintDocumentAdapter : PrintDocumentAdapter
{
    private readonly string _filePath;
    private readonly string _jobName;

    public PdfFilePrintDocumentAdapter(string filePath, string jobName)
    {
        _filePath = filePath;
        _jobName = jobName;
    }

    public override void OnLayout(
        PrintAttributes? oldAttributes,
        PrintAttributes? newAttributes,
        CancellationSignal? cancellationSignal,
        LayoutResultCallback? callback,
        Bundle? extras)
    {
        if (cancellationSignal?.IsCanceled == true)
        {
            callback?.OnLayoutCancelled();
            return;
        }

        var info = new PrintDocumentInfo.Builder(_jobName)
            .SetContentType(PrintContentType.Document)
            .SetPageCount(PrintDocumentInfo.PageCountUnknown)
            .Build();

        // The content does not depend on the chosen print attributes.
        callback?.OnLayoutFinished(info, false);
    }

    public override void OnWrite(
        PageRange[]? pages,
        ParcelFileDescriptor? destination,
        CancellationSignal? cancellationSignal,
        WriteResultCallback? callback)
    {
        if (destination is null)
        {
            callback?.OnWriteFailed("No output destination was provided.");
            return;
        }

        try
        {
            using var input = new FileInputStream(_filePath);
            using var output = new FileOutputStream(destination.FileDescriptor!);

            var buffer = new byte[16 * 1024];
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                if (cancellationSignal?.IsCanceled == true)
                {
                    callback?.OnWriteCancelled();
                    return;
                }

                output.Write(buffer, 0, read);
            }

            output.Flush();
            callback?.OnWriteFinished(new[] { PageRange.AllPages! });
        }
        catch (Java.IO.IOException ex)
        {
            callback?.OnWriteFailed(ex.Message);
        }
    }
}
#endif
