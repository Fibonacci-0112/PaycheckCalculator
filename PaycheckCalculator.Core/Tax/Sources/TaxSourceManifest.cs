namespace PaycheckCalculator.Core.Tax.Sources;

/// <summary>Root object for one tax year's canonical source manifest.</summary>
public sealed class TaxSourceManifest
{
    public int TaxYear { get; init; }
    public IReadOnlyList<TaxSourceRule> Rules { get; init; } = Array.Empty<TaxSourceRule>();
}
