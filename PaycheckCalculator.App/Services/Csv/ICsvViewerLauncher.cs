namespace PaycheckCalculator.App.Services.Csv;

/// <summary>
/// Opens a CSV file in the platform's default application for CSV (e.g. Microsoft
/// Excel on Windows, or whichever spreadsheet app the user has set as the default
/// CSV handler on Android).
/// </summary>
public interface ICsvViewerLauncher
{
    Task OpenCsvAsync(string filePath);
}
