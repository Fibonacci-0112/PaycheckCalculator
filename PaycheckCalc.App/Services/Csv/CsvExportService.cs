using System.Globalization;
using System.Text;
using Microsoft.Maui.Storage;
using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Csv;

/// <inheritdoc />
public sealed class CsvExportService : ICsvExportService
{
    private readonly IShareLauncher _share;

    public CsvExportService(IShareLauncher share) => _share = share;

    /// <inheritdoc />
    public async Task<string> ExportAndShareAsync(ResultCardModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var csv = PaycheckCsvRenderer.Render(result);

        // Write under the same "sharing-root" cache sub-directory the PDF export
        // uses, as recommended for Android FileProvider sharing.
        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"Paycheck-Summary-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv";
        var path = Path.Combine(directory, fileName);

        // UTF-8 without a BOM; the content is ASCII in practice.
        await File.WriteAllTextAsync(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await _share.ShareAsync(path, "Paycheck Summary (CSV)");
        return path;
    }
}
