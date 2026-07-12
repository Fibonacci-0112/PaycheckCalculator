namespace PaycheckCalculator.App.Services.Pdf;

/// <summary>
/// Writes a rendered PDF to a shareable location on disk and opens it for viewing.
/// The caller renders the bytes (via <see cref="PaycheckPdfRenderer"/>) so it can
/// choose what to include — per-period results, the annual projection, and/or an
/// A/B comparison.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Writes <paramref name="pdfBytes"/> to a shareable location named after
    /// <paramref name="baseFileName"/> (sanitized; <c>.pdf</c> appended) and launches it
    /// in a PDF viewer (preferring Adobe Reader/Acrobat). Returns the file path.
    /// </summary>
    Task<string> ExportAndOpenAsync(byte[] pdfBytes, string baseFileName);
}
