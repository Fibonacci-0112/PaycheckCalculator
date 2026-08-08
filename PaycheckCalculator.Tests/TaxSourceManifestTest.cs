using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Sources;
using Xunit;

namespace PaycheckCalculator.Tests;

public sealed class TaxSourceManifestTest
{
    [Fact]
    public void Manifest_HasStrictMetadataAndRegularCoverageForEveryJurisdiction()
    {
        var catalog = LoadCatalog();

        Assert.Equal(2026, catalog.TaxYear);
        Assert.Equal(catalog.Rules.Count, catalog.Rules.Select(rule => rule.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var rule in catalog.Rules)
        {
            Assert.Equal(2026, rule.TaxYear);
            Assert.StartsWith("https://", rule.OfficialUrl, StringComparison.Ordinal);
            Assert.NotEmpty(rule.PublicationTitle);
            Assert.NotEmpty(rule.CalculatorClass);
            Assert.NotEqual(default, rule.LastVerificationDate);
            Assert.True(rule.RevisionDate is not null || rule.EffectiveDate is not null);
            Assert.NotNull(rule.Approximations);
            Assert.NotNull(rule.Exclusions);
        }

        foreach (var state in Enum.GetValues<UsState>())
        {
            Assert.Single(catalog.Find(
                state,
                2026,
                TaxRuleScope.RegularWithholding,
                TaxCalculationMode.Standard));
        }
    }

    [Fact]
    public void Manifest_HasRequiredFederalScopesAndDisclosures()
    {
        var catalog = LoadCatalog();
        TaxRuleScope[] required =
        [
            TaxRuleScope.FederalWithholding,
            TaxRuleScope.SocialSecurityMedicare,
            TaxRuleScope.AdditionalMedicare,
            TaxRuleScope.SupplementalWithholding,
            TaxRuleScope.SelfEmploymentTax,
            TaxRuleScope.EstimatedPayments
        ];

        foreach (var scope in required)
            Assert.Contains(catalog.Rules, rule => rule.Jurisdiction == "US" && rule.Scope == scope);

        Assert.Contains(
            catalog.Rules.SelectMany(rule => rule.Exclusions),
            note => note.Contains("Form 1040", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            catalog.Rules.SelectMany(rule => rule.Exclusions),
            note => note.Contains("federal income tax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Manifest_RejectsDuplicateIdsMissingMetadataAndNonIsoDates()
    {
        var root = JsonNode.Parse(File.ReadAllText("tax_source_manifest_2026.json"))!.AsObject();
        var rules = root["rules"]!.AsArray();

        rules[1]!["id"] = rules[0]!["id"]!.GetValue<string>();
        Assert.Throws<InvalidOperationException>(() => TaxSourceCatalog.Load(root.ToJsonString()));

        root = JsonNode.Parse(File.ReadAllText("tax_source_manifest_2026.json"))!.AsObject();
        rules = root["rules"]!.AsArray();
        rules[0]!.AsObject().Remove("officialUrl");
        Assert.Throws<InvalidOperationException>(() => TaxSourceCatalog.Load(root.ToJsonString()));

        root = JsonNode.Parse(File.ReadAllText("tax_source_manifest_2026.json"))!.AsObject();
        root["rules"]![0]!["lastVerificationDate"] = "08/08/2026";
        Assert.Throws<InvalidOperationException>(() => TaxSourceCatalog.Load(root.ToJsonString()));
    }

    [Fact]
    public void RuntimeRegistryMatchesManifestCalculatorClasses()
    {
        using var provider = CreateServices();
        var catalog = provider.GetRequiredService<TaxSourceCatalog>();
        var registry = provider.GetRequiredService<PaycheckCalculator.Core.Tax.State.StateCalculatorRegistry>();

        catalog.ValidateCalculatorRegistrations(registry);
    }

    [Fact]
    public void StandardBonusAndSelfEmployment_AttachOnlyActiveStateAndModeSources()
    {
        using var provider = CreateServices();

        var paycheck = provider.GetRequiredService<PayCalculator>().Calculate(new PaycheckInput
        {
            State = UsState.TX,
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m
        });
        Assert.Contains(paycheck.Explanation.Sources, source => source.RuleId == "state-tx-regular-2026");
        Assert.DoesNotContain(paycheck.Explanation.Sources, source => source.RuleId?.StartsWith("state-ca-", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(paycheck.Explanation.Sources, source => source.RuleId?.Contains("supplemental", StringComparison.Ordinal) == true);

        var bonus = provider.GetRequiredService<BonusCalculator>().Calculate(new BonusInput
        {
            State = UsState.CA,
            BonusAmount = 5_000m
        });
        Assert.Contains(bonus.Explanation.Sources, source => source.RuleId == "state-ca-supplemental-2026");
        Assert.DoesNotContain(bonus.Explanation.Sources, source => source.RuleId == "state-ca-regular-2026");

        var selfEmployment = provider.GetRequiredService<SelfEmploymentCalculator>().Calculate(new SelfEmploymentInput
        {
            State = UsState.TX,
            AnnualNetEarnings = 100_000m
        });
        Assert.Contains(selfEmployment.Explanation.Sources, source => source.RuleId == "federal-schedule-se-2026");
        Assert.Contains(selfEmployment.Explanation.Sources, source => source.RuleId == "federal-form-1040es-2026");
        Assert.Contains(selfEmployment.Explanation.Sources, source => source.RuleId == "state-tx-regular-2026");
        Assert.DoesNotContain(selfEmployment.Explanation.Sources, source => source.RuleId?.Contains("supplemental", StringComparison.Ordinal) == true);
    }

    private static TaxSourceCatalog LoadCatalog() =>
        TaxSourceCatalog.Load(File.ReadAllText("tax_source_manifest_2026.json"));

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddPaycheckCalculatorCore(new OutputTaxDataReader());
        return services.BuildServiceProvider();
    }

    private sealed class OutputTaxDataReader : ITaxDataReader
    {
        public string ReadAllText(string logicalName)
        {
            var relative = logicalName.Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(AppContext.BaseDirectory, relative);
            if (!File.Exists(path) && relative.StartsWith($"schemas{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                path = Path.Combine(AppContext.BaseDirectory, "Schemas", relative[8..]);
            return File.ReadAllText(path);
        }
    }
}
