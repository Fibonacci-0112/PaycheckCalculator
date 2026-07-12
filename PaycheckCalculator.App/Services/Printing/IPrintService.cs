namespace PaycheckCalculator.App.Services.Printing;

/// <summary>
/// Sends a rendered paycheck document to the platform print system. The caller
/// renders the PDF bytes (via <see cref="Pdf.PaycheckPdfRenderer"/>) so the printed
/// page matches the exported PDF.
/// </summary>
public interface IPrintService
{
    /// <summary>
    /// Hands <paramref name="pdfBytes"/> to the platform print framework (the Android
    /// print dialog or the Windows default print handler), using <paramref name="jobName"/>
    /// as the print job title.
    /// </summary>
    Task PrintAsync(byte[] pdfBytes, string jobName);
}
