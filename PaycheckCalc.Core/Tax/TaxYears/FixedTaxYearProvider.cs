namespace PaycheckCalc.Core.Tax.TaxYears;

/// <summary>Default tax-year provider for the currently bundled tax tables.</summary>
public sealed class FixedTaxYearProvider : ITaxYearProvider
{
    public const int BundledTaxYear = 2026;

    public FixedTaxYearProvider(int currentTaxYear = BundledTaxYear)
    {
        CurrentTaxYear = currentTaxYear;
    }

    public int CurrentTaxYear { get; }
}
