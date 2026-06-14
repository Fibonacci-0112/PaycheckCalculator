namespace PaycheckCalc.App.Services.Csv;

/// <summary>
/// Writes rendered CSV text to a shareable location on disk and opens it in the
/// platform's default CSV application (e.g. Microsoft Excel on Windows, or the
/// user's chosen default spreadsheet app on Android). The caller renders the CSV
/// (via <see cref="PaycheckCsvRenderer"/>) so it can choose what to include.
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Writes <paramref name="csv"/> to a shareable location named after
    /// <paramref name="baseFileName"/> (sanitized; <c>.csv</c> appended) and opens it in the
    /// default CSV application. Returns the file path.
    /// </summary>
    Task<string> ExportAndOpenAsync(string csv, string baseFileName);
}
