using System.Globalization;
using Microsoft.Maui.Storage;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services.Pdf;

namespace PaycheckCalc.App.Services.Printing;

/// <inheritdoc />
public sealed class PrintService : IPrintService
{
    private readonly IPrintLauncher _launcher;

    public PrintService(IPrintLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task PrintAsync(ResultCardModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Reuse the PDF layout so the printed page matches the exported PDF.
        var pdfBytes = PaycheckPdfRenderer.Render(result);

        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"Paycheck-Print-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.pdf";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, pdfBytes);

        await _launcher.PrintAsync(path, "Paycheck Summary");
    }
}
