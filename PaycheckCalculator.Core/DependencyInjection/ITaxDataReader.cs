namespace PaycheckCalculator.Core.DependencyInjection;

public interface ITaxDataReader
{
    // Returns the raw JSON for a logical name like "ar_withholding_2026.json"
    // or "schemas/ca.json". Should throw FileNotFoundException when absent so
    // the per-state schema loop can swallow missing files.
    string ReadAllText(string logicalName);
}
