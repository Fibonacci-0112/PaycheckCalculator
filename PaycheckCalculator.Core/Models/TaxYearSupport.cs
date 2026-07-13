namespace PaycheckCalculator.Core.Models;

/// <summary>
/// Identifies the single tax year whose tables are compiled into this build of the
/// Core engine.  All calculators run against this year's IRS/state rules.
/// </summary>
public static class TaxYearSupport
{
    /// <summary>The tax year whose rules and tables are active in the current build (2026).</summary>
    public const int CurrentTaxYear = 2026;

    /// <summary>All tax years supported by this build (only <see cref="CurrentTaxYear"/> in practice).</summary>
    public static IReadOnlyList<int> SupportedTaxYears { get; } = new[] { CurrentTaxYear };
}
