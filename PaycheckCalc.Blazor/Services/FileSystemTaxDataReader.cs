using System.Text;
using PaycheckCalc.Core.DependencyInjection;

namespace PaycheckCalc.Blazor.Services;

public sealed class FileSystemTaxDataReader : ITaxDataReader
{
    private readonly string _basePath;

    public FileSystemTaxDataReader(string basePath)
    {
        _basePath = basePath;
    }

    public string ReadAllText(string logicalName)
    {
        var path = Path.Combine(_basePath, logicalName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Tax data file not found: {logicalName}", path);
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
