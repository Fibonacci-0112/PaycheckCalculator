using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <inheritdoc />
public sealed class PdfExportService : IPdfExportService
{
    // Resolution of the off-screen chart bitmap. The doughnut drawable uses
    // fixed pixel font sizes for its legend, so this size also controls how
    // large that text appears once the image is scaled to the page width.
    private const int ChartPixelWidth = 760;
    private const int ChartPixelHeight = 600;

    private readonly IPdfViewerLauncher _launcher;

    public PdfExportService(IPdfViewerLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task<string> ExportAndOpenAsync(ResultCardModel result, AnnualProjectionModel? projection)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Drawing into a platform bitmap context must happen on the UI thread.
        var chart = await MainThread.InvokeOnMainThreadAsync(
            () => ChartImageRenderer.Render(result, ChartPixelWidth, ChartPixelHeight));

        var pdfBytes = PaycheckPdfRenderer.Render(result, projection, chart);

        // Write under a dedicated "sharing-root" sub-directory of the cache, as
        // recommended for Android FileProvider sharing.
        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"Paycheck-Summary-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.pdf";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, pdfBytes);

        await _launcher.OpenPdfAsync(path);
        return path;
    }
}
