using PaycheckCalc.App.Models;

namespace PaycheckCalc.App.Services.Pdf;

/// <summary>
/// Exports the current per-period paycheck results to a single-page PDF on disk
/// and opens it for viewing.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Builds the PDF, writes it to a shareable location, and launches it in a
    /// PDF viewer (preferring Adobe Reader/Acrobat). Returns the file path.
    /// </summary>
    Task<string> ExportAndOpenAsync(ResultCardModel result);
}
