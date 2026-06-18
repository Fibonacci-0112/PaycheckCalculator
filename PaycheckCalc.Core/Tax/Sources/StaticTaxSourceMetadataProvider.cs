using PaycheckCalc.Core.Tax.TaxYears;

namespace PaycheckCalc.Core.Tax.Sources;

/// <summary>Metadata for the bundled tax data files and calculator constants.</summary>
public sealed class StaticTaxSourceMetadataProvider : ITaxSourceMetadataProvider
{
    private static readonly TaxSourceMetadata[] Sources =
    [
        new(FixedTaxYearProvider.BundledTaxYear, "Federal", "IRS Publication 15-T percentage method tables", "https://www.irs.gov/publications/p15t", new DateOnly(2026, 1, 1), "Federal withholding uses the automated payroll systems percentage method data bundled with the app."),
        new(FixedTaxYearProvider.BundledTaxYear, "Federal", "Social Security Administration contribution and benefit base", "https://www.ssa.gov/oact/cola/cbb.html", new DateOnly(2026, 1, 1), "FICA uses the bundled Social Security wage base and statutory Medicare thresholds."),
        new(FixedTaxYearProvider.BundledTaxYear, "States + DC", "State employer withholding publications and forms", "https://github.com/", new DateOnly(2026, 1, 1), "State calculators cite jurisdiction-specific withholding guides in their Show Your Work references."),
    ];

    public IReadOnlyList<TaxSourceMetadata> GetSources(int taxYear)
        => Sources.Where(s => s.TaxYear == taxYear).ToArray();
}
