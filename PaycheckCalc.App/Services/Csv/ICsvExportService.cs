using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Csv;

/// <summary>
/// Exports the current per-period paycheck results to a CSV file on disk and
/// opens it in the platform's default CSV application (e.g. Microsoft Excel on
/// Windows, or the user's chosen default spreadsheet app on Android).
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Builds the CSV, writes it to a shareable location, and opens it in the
    /// default CSV application. Returns the file path.
    /// </summary>
    Task<string> ExportAndOpenAsync(ResultCardModel result);
}
