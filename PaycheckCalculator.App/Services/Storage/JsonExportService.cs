using System.Text;
using Microsoft.Maui.Storage;
using PaycheckCalculator.App.Helpers;

namespace PaycheckCalculator.App.Services.Storage;

/// <inheritdoc />
public sealed class JsonExportService : IJsonExportService
{
    public async Task<string> ExportAndOpenAsync(string json, string baseFileName)
    {
        ArgumentNullException.ThrowIfNull(json);

        var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
        Directory.CreateDirectory(directory);

        var fileName = $"{FileNameSanitizer.Sanitize(baseFileName)}.json";
        var path = Path.Combine(directory, fileName);

        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await Launcher.Default.OpenAsync(new OpenFileRequest("Account data export", new ReadOnlyFile(path)));
        return path;
    }
}
