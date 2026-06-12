using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Csv;

/// <summary>
/// Exports the current per-period paycheck results to a CSV file on disk and
/// offers it to the user via the platform share sheet (so it can be opened in a
/// spreadsheet app, emailed, or saved to files).
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Builds the CSV, writes it to a shareable location, and opens the share
    /// sheet. Returns the file path.
    /// </summary>
    Task<string> ExportAndShareAsync(ResultCardModel result);
}
