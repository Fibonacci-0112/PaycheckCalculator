using System.IO;
using PaycheckCalculator.Core.Tax.Maryland;

/// <summary>
/// Test-only access to the shipping Maryland county rate table, loaded from the
/// <c>md_county_rates_2026.json</c> copied into the test bin output, so expected
/// values in tests are the same rates the app applies.
/// </summary>
public static class TestMarylandCountyRates
{
    public static MarylandCountyRates Table { get; } = MarylandCountyRates.Load(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "md_county_rates_2026.json")));
}
