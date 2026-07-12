using PaycheckCalculator.Core.DependencyInjection;

namespace PaycheckCalculator.App.Services;

public sealed class MauiAppPackageTaxDataReader : ITaxDataReader
{
    public string ReadAllText(string logicalName)
    {
        using var stream = FileSystem.OpenAppPackageFileAsync(logicalName).Result;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
