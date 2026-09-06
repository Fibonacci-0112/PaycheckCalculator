using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Sources;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Guards the seam between a calculator that withholds a payroll assessment and
/// the source manifest that has to cite it. Nothing in the engine enforces this:
/// <c>PayCalculator</c> attaches <see cref="TaxRuleScope.PayrollAssessment"/>
/// rule IDs by jurisdiction, so a state that starts withholding a premium
/// without a matching manifest rule would silently show an uncited line — and
/// its accuracy notes, which are derived from the cited rules, would vanish.
/// </summary>
public sealed class StatePayrollAssessmentCoverageTest
{
    [Fact]
    public void EveryStateThatWithholdsAnAssessment_HasAManifestRuleCitingIt()
    {
        using var provider = CreateServices();
        var registry = provider.GetRequiredService<StateCalculatorRegistry>();
        var catalog = provider.GetRequiredService<TaxSourceCatalog>();
        var schemas = provider.GetRequiredService<IStateSchemaProvider>();

        var missing = new List<string>();
        foreach (var state in registry.SupportedStates)
        {
            var values = PayCalculatorTestHarness.DefaultValues(schemas.GetSchema(state));
            var context = new CommonWithholdingContext(
                state, GrossWages: 5_000m, PayPeriod: PayFrequency.Biweekly, Year: 2026);

            if (registry.GetCalculator(state).Calculate(context, values).DisabilityInsurance <= 0m)
                continue;

            var ruleIds = catalog.RuleIds(
                state, 2026, TaxRuleScope.PayrollAssessment, TaxCalculationMode.Standard);
            if (ruleIds.Count == 0)
                missing.Add(state.ToString());
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPayrollAssessmentRule_NamesTheRegisteredCalculator()
    {
        using var provider = CreateServices();
        var registry = provider.GetRequiredService<StateCalculatorRegistry>();
        var catalog = provider.GetRequiredService<TaxSourceCatalog>();

        var mismatched = new List<string>();
        foreach (var rule in catalog.Rules.Where(r => r.Scope == TaxRuleScope.PayrollAssessment))
        {
            if (!Enum.TryParse<UsState>(rule.Jurisdiction, ignoreCase: true, out var state))
            {
                mismatched.Add($"{rule.Id}: unknown jurisdiction");
                continue;
            }

            var actual = registry.GetCalculator(state).GetType().Name;
            if (!string.Equals(rule.CalculatorClass, actual, StringComparison.Ordinal))
                mismatched.Add($"{rule.Id}: names '{rule.CalculatorClass}' but '{actual}' is registered");
        }

        Assert.Empty(mismatched);
    }

    [Fact]
    public void AssessmentStates_AreCoveredByTheRateTable()
    {
        // The six states added alongside CA/CO/CT/WA all load their rates from
        // the shared table rather than hardcoded constants.
        using var provider = CreateServices();
        var assessments = provider.GetRequiredService<StatePayrollAssessments>();

        foreach (var state in new[] { UsState.NJ, UsState.NY, UsState.MA, UsState.RI, UsState.OR, UsState.HI })
            Assert.NotEmpty(assessments.For(state));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddPaycheckCalculatorCore(new TestTaxDataReader());
        return services.BuildServiceProvider();
    }

    private sealed class TestTaxDataReader : ITaxDataReader
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
