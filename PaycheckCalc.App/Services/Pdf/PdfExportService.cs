using System.Globalization;
using Microsoft.Maui.Storage;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <inheritdoc />
public sealed class PdfExportService : IPdfExportService
{
    private readonly IPdfViewerLauncher _launcher;

    public PdfExportService(IPdfViewerLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task<string> ExportAndOpenAsync(ResultCardModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var pdfBytes = PaycheckPdfRenderer.Render(result);

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
