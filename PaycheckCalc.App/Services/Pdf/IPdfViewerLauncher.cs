namespace PaycheckCalc.App.Services.Pdf;

/// <summary>
/// Opens a PDF file in an external viewer, preferring Adobe Reader/Acrobat and
/// falling back to the platform's default PDF handler when Adobe isn't present.
/// </summary>
public interface IPdfViewerLauncher
{
    Task OpenPdfAsync(string filePath);
}
