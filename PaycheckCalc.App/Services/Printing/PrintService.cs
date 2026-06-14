using Microsoft.Maui.Storage;
using PaycheckCalc.App.Helpers;

namespace PaycheckCalc.App.Services.Printing;

/// <inheritdoc />
public sealed class PrintService : IPrintService
{
    private readonly IPrintLauncher _launcher;

    public PrintService(IPrintLauncher launcher) => _launcher = launcher;

    /// <inheritdoc />
    public async Task PrintAsync(byte[] pdfBytes, string jobName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"{FileNameSanitizer.Sanitize(jobName, "Paycheck-Print")}.pdf";
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, pdfBytes);

        var title = string.IsNullOrWhiteSpace(jobName) ? "Paycheck Summary" : jobName.Trim();
        await _launcher.PrintAsync(path, title);
    }
}
