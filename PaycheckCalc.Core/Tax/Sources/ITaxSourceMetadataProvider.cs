namespace PaycheckCalc.Core.Tax.Sources;

public interface ITaxSourceMetadataProvider
{
    IReadOnlyList<TaxSourceMetadata> GetSources(int taxYear);
}
