namespace PaycheckCalculator.App.Services.Storage;

/// <summary>Writes JSON to a shareable file and opens it in the platform default app.</summary>
public interface IJsonExportService
{
    Task<string> ExportAndOpenAsync(string json, string baseFileName);
}
