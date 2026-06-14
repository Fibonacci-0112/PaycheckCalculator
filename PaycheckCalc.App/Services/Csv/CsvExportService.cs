using System.Text;
using Microsoft.Maui.Storage;
using PaycheckCalc.App.Helpers;

namespace PaycheckCalc.App.Services.Csv;

/// <inheritdoc />
public sealed class CsvExportService : ICsvExportService
{
    private readonly ICsvViewerLauncher _launcher;

    public CsvExportService(ICsvViewerLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task<string> ExportAndOpenAsync(string csv, string baseFileName)
    {
        ArgumentNullException.ThrowIfNull(csv);

        // Write under the same "sharing-root" cache sub-directory the PDF export
        // uses, as recommended for Android FileProvider sharing.
        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"{FileNameSanitizer.Sanitize(baseFileName)}.csv";
        var path = Path.Combine(directory, fileName);

        // UTF-8 without a BOM; the content is ASCII in practice.
        await File.WriteAllTextAsync(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await _launcher.OpenCsvAsync(path);
        return path;
    }
}
