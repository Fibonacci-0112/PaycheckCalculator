namespace PaycheckCalc.Core.Tax.TaxYears;

/// <summary>Provides the tax year used for new calculations and migrated snapshots.</summary>
public interface ITaxYearProvider
{
    int CurrentTaxYear { get; }
}
