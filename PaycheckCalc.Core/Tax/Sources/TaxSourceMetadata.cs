namespace PaycheckCalc.Core.Tax.Sources;

/// <summary>Source and freshness metadata for a tax table used by the calculator.</summary>
public sealed record TaxSourceMetadata(
    int TaxYear,
    string Jurisdiction,
    string SourceName,
    string SourceUrl,
    DateOnly RetrievedOn,
    string Notes);
