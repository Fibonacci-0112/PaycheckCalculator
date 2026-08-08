using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.Sources;

/// <summary>A calculation scope documented by the tax-source manifest.</summary>
public enum TaxRuleScope
{
    FederalWithholding,
    SocialSecurityMedicare,
    AdditionalMedicare,
    RegularWithholding,
    PayrollAssessment,
    SupplementalWithholding,
    SelfEmploymentTax,
    EstimatedPayments
}

/// <summary>A calculator mode to which a source rule applies.</summary>
public enum TaxCalculationMode
{
    Standard,
    GrossUp,
    AnnualProjection,
    Bonus,
    SelfEmployment
}

/// <summary>How the cited rule is represented by the calculation engine.</summary>
public enum TaxRuleImplementationType
{
    JsonTable,
    CodedFormula,
    FlatRate,
    PercentageElection,
    NoIncomeTaxAdapter,
    UnsupportedRegularAggregateSupplementalMethod
}

/// <summary>One official publication/rule record from the canonical source manifest.</summary>
public sealed class TaxSourceRule
{
    public string Id { get; init; } = "";
    public string Jurisdiction { get; init; } = "";
    public int TaxYear { get; init; }
    public TaxRuleScope Scope { get; init; }
    public IReadOnlyList<TaxCalculationMode> CalculationModes { get; init; } = Array.Empty<TaxCalculationMode>();
    public string PublicationTitle { get; init; } = "";
    public string OfficialUrl { get; init; } = "";
    public DateOnly? RevisionDate { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateOnly LastVerificationDate { get; init; }
    public TaxRuleImplementationType ImplementationType { get; init; }
    public string CalculatorClass { get; init; } = "";
    public IReadOnlyList<string> Approximations { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclusions { get; init; } = Array.Empty<string>();
    public string ApplicabilityNotes { get; init; } = "";

    public bool AppliesTo(UsState state) =>
        string.Equals(Jurisdiction, state.ToString(), StringComparison.OrdinalIgnoreCase);
}
