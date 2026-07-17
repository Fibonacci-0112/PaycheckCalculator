using System.Text;
using Microsoft.Maui.Storage;
using PaycheckCalculator.App.Helpers;

namespace PaycheckCalculator.App.Services.Storage;

/// <inheritdoc />
public sealed class JsonExportService : IJsonExportService
{
    /// <summary>
    /// Writes the JSON payload to a sanitized file name under the app cache sharing directory,
    /// opens it in the platform default file handler, and returns the full exported path.
    /// </summary>
    public async Task<string> ExportAndOpenAsync(string json, string baseFileName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFileName);

        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"{FileNameSanitizer.Sanitize(baseFileName)}.json";
        var path = Path.Combine(directory, fileName);

        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await Launcher.Default.OpenAsync(new OpenFileRequest("Account data export", new ReadOnlyFile(path)));
        return path;
    }
}
