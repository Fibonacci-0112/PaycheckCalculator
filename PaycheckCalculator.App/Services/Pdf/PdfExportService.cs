using Microsoft.Maui.Storage;
using PaycheckCalculator.App.Helpers;

namespace PaycheckCalculator.App.Services.Pdf;

/// <inheritdoc />
public sealed class PdfExportService : IPdfExportService
{
    private readonly IPdfViewerLauncher _launcher;

    public PdfExportService(IPdfViewerLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task<string> ExportAndOpenAsync(byte[] pdfBytes, string baseFileName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        // Write under a dedicated "sharing-root" sub-directory of the cache, as
        // recommended for Android FileProvider sharing.
        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"{FileNameSanitizer.Sanitize(baseFileName)}.pdf";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, pdfBytes);

        await _launcher.OpenPdfAsync(path);
        return path;
    }
}
