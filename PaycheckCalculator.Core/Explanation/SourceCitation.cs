using PaycheckCalculator.Core.Tax.Sources;

namespace PaycheckCalculator.Core.Explanation;

/// <summary>A structured citation to an official rule used by a calculation line.</summary>
public sealed record SourceCitation
{
    /// <summary>Compatibility constructor for legacy free-form references.</summary>
    public SourceCitation(string label, string reference)
    {
        Label = label;
        Reference = reference;
        PublicationTitle = reference;
    }

    internal SourceCitation(string label, TaxSourceRule rule)
    {
        Label = label;
        RuleId = rule.Id;
        PublicationTitle = rule.PublicationTitle;
        OfficialUrl = rule.OfficialUrl;
        TaxYear = rule.TaxYear;
        RevisionDate = rule.RevisionDate;
        EffectiveDate = rule.EffectiveDate;
        LastVerificationDate = rule.LastVerificationDate;
        ImplementationType = rule.ImplementationType;
        Approximations = rule.Approximations;
        Exclusions = rule.Exclusions;
        ApplicabilityNotes = rule.ApplicabilityNotes;
        Reference = $"{rule.PublicationTitle} ({rule.TaxYear})";
    }

    public string Label { get; }
    public string Reference { get; }
    public string? RuleId { get; }
    public string PublicationTitle { get; }
    public string? OfficialUrl { get; }
    public int? TaxYear { get; }
    public DateOnly? RevisionDate { get; }
    public DateOnly? EffectiveDate { get; }
    public DateOnly? LastVerificationDate { get; }
    public TaxRuleImplementationType? ImplementationType { get; }
    public IReadOnlyList<string> Approximations { get; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclusions { get; } = Array.Empty<string>();
    public string? ApplicabilityNotes { get; }
}
