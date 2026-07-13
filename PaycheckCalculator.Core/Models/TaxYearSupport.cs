namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Centralizes which tax year(s) the loaded tax data supports. All federal/state tax data
/// currently ships for a single year (2026); this becomes the single place to update — and,
/// once a second year is added, the place to turn into a real registry lookup — rather than a
/// literal scattered across calculators.
/// </summary>
public static class TaxYearSupport
{
    /// <summary>The tax year new inputs default to, and the only year currently supported.</summary>
    public const int Default = 2026;

    /// <summary>True when tax data for the given year is loaded and can be calculated against.</summary>
    public static bool IsSupported(int taxYear) => taxYear == Default;
}
