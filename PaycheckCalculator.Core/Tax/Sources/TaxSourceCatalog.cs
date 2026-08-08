using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.Sources;

/// <summary>
/// Strict, UI-independent catalog of the official publications used by the calculation engine.
/// </summary>
public sealed class TaxSourceCatalog
{
    private readonly IReadOnlyDictionary<string, TaxSourceRule> _byId;

    private TaxSourceCatalog(TaxSourceManifest manifest)
    {
        TaxYear = manifest.TaxYear;
        Rules = manifest.Rules;
        _byId = Rules.ToDictionary(rule => rule.Id, StringComparer.OrdinalIgnoreCase);
    }

    public int TaxYear { get; }
    public IReadOnlyList<TaxSourceRule> Rules { get; }

    public static TaxSourceCatalog Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ValidateRawMetadata(json);

        TaxSourceManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<TaxSourceManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            }) ?? throw new InvalidOperationException("The tax source manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The tax source manifest is malformed.", ex);
        }

        Validate(manifest);
        return new TaxSourceCatalog(manifest);
    }

    public TaxSourceRule GetById(string id) =>
        _byId.TryGetValue(id, out var rule)
            ? rule
            : throw new KeyNotFoundException($"Tax source rule '{id}' was not found.");

    public IReadOnlyList<TaxSourceRule> Find(
        string jurisdiction,
        int taxYear,
        TaxRuleScope scope,
        TaxCalculationMode mode) =>
        Rules.Where(rule =>
                rule.TaxYear == taxYear
                && rule.Scope == scope
                && rule.CalculationModes.Contains(mode)
                && string.Equals(rule.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<TaxSourceRule> Find(
        UsState state,
        int taxYear,
        TaxRuleScope scope,
        TaxCalculationMode mode) =>
        Find(state.ToString(), taxYear, scope, mode);

    public IReadOnlyList<string> RuleIds(
        string jurisdiction,
        int taxYear,
        TaxRuleScope scope,
        TaxCalculationMode mode) =>
        Find(jurisdiction, taxYear, scope, mode).Select(rule => rule.Id).ToList();

    public IReadOnlyList<string> RuleIds(
        UsState state,
        int taxYear,
        TaxRuleScope scope,
        TaxCalculationMode mode) =>
        Find(state, taxYear, scope, mode).Select(rule => rule.Id).ToList();

    internal SourceCitation CreateCitation(string label, TaxSourceRule rule) =>
        new(label, rule);

    public void ValidateCalculatorRegistrations(StateCalculatorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        foreach (var state in Enum.GetValues<UsState>())
        {
            var rule = Rules.Single(candidate =>
                candidate.AppliesTo(state)
                && candidate.Scope == TaxRuleScope.RegularWithholding);
            var actual = registry.GetCalculator(state).GetType().Name;
            if (!string.Equals(rule.CalculatorClass, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Tax source rule '{rule.Id}' names calculator '{rule.CalculatorClass}', but '{actual}' is registered.");
            }
        }
    }

    private static void Validate(TaxSourceManifest manifest)
    {
        if (!TaxYearSupport.IsSupported(manifest.TaxYear))
            throw new InvalidOperationException($"Tax source manifest year {manifest.TaxYear} is not supported.");
        if (manifest.Rules is null || manifest.Rules.Count == 0)
            throw new InvalidOperationException("The tax source manifest contains no rules.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in manifest.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id)
                || string.IsNullOrWhiteSpace(rule.Jurisdiction)
                || string.IsNullOrWhiteSpace(rule.PublicationTitle)
                || string.IsNullOrWhiteSpace(rule.OfficialUrl)
                || string.IsNullOrWhiteSpace(rule.CalculatorClass)
                || string.IsNullOrWhiteSpace(rule.ApplicabilityNotes))
            {
                throw new InvalidOperationException("Every tax source rule requires an ID, jurisdiction, publication, URL, calculator class, and applicability notes.");
            }
            if (!ids.Add(rule.Id))
                throw new InvalidOperationException($"Duplicate tax source rule ID '{rule.Id}'.");
            if (rule.TaxYear != manifest.TaxYear || !TaxYearSupport.IsSupported(rule.TaxYear))
                throw new InvalidOperationException($"Rule '{rule.Id}' has unsupported tax year {rule.TaxYear}.");
            if (rule.CalculationModes is null || rule.CalculationModes.Count == 0)
                throw new InvalidOperationException($"Rule '{rule.Id}' has no calculation modes.");
            if (!Enum.IsDefined(rule.Scope)
                || !Enum.IsDefined(rule.ImplementationType)
                || rule.CalculationModes.Any(mode => !Enum.IsDefined(mode)))
            {
                throw new InvalidOperationException($"Rule '{rule.Id}' uses an unsupported controlled value.");
            }
            if (rule.Approximations is null || rule.Exclusions is null)
                throw new InvalidOperationException($"Rule '{rule.Id}' requires approximation and exclusion metadata.");
            if (rule.RevisionDate is null && rule.EffectiveDate is null)
                throw new InvalidOperationException($"Rule '{rule.Id}' requires a revision date or effective date.");
            if (rule.LastVerificationDate == default)
                throw new InvalidOperationException($"Rule '{rule.Id}' requires a last verification date.");
            if (!Uri.TryCreate(rule.OfficialUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"Rule '{rule.Id}' must use an absolute official HTTPS URL.");
            }
            if (!string.Equals(rule.Jurisdiction, "US", StringComparison.OrdinalIgnoreCase)
                && !Enum.TryParse<UsState>(rule.Jurisdiction, true, out _))
            {
                throw new InvalidOperationException($"Rule '{rule.Id}' has unknown jurisdiction '{rule.Jurisdiction}'.");
            }
        }

        foreach (var state in Enum.GetValues<UsState>())
        {
            if (!manifest.Rules.Any(rule =>
                    rule.AppliesTo(state)
                    && rule.Scope == TaxRuleScope.RegularWithholding
                    && rule.CalculationModes.Contains(TaxCalculationMode.Standard)))
            {
                throw new InvalidOperationException($"Tax source manifest is missing regular-withholding coverage for {state}.");
            }
        }

        private static void ValidateRawMetadata(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("rules", out var rules)
                || rules.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("The tax source manifest requires a rules array.");
            }

            string[] required =
            [
                "id", "jurisdiction", "taxYear", "scope", "calculationModes",
                "publicationTitle", "officialUrl", "lastVerificationDate",
                "implementationType", "calculatorClass", "approximations",
                "exclusions", "applicabilityNotes"
            ];
            foreach (var rule in rules.EnumerateArray())
            {
                foreach (var property in required)
                {
                    if (!rule.TryGetProperty(property, out _))
                        throw new InvalidOperationException($"Every tax source rule requires '{property}'.");
                }
                if (!rule.TryGetProperty("revisionDate", out _)
                    && !rule.TryGetProperty("effectiveDate", out _))
                {
                    throw new InvalidOperationException("Every tax source rule requires a revisionDate or effectiveDate.");
                }

                foreach (var dateProperty in new[] { "revisionDate", "effectiveDate", "lastVerificationDate" })
                {
                    if (!rule.TryGetProperty(dateProperty, out var value)) continue;
                    var text = value.GetString();
                    if (text is null
                        || text.Length != 10
                        || !DateOnly.TryParseExact(
                            text,
                            "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out _))
                    {
                        throw new InvalidOperationException($"Tax source date '{dateProperty}' must use ISO yyyy-MM-dd format.");
                    }
                }
            }
        }
    }
}
