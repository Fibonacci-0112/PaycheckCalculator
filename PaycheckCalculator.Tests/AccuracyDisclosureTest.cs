using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Sources;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// "Honest about scope" is a stated product principle, and standard-mode accuracy
/// notes are derived from the cited manifest rules rather than hand-written — so a
/// disclosure regresses silently if a rule stops being cited or its text drifts
/// back to describing the publication instead of what the calculator does.
/// </summary>
public sealed class AccuracyDisclosureTest
{
    [Theory]
    // States that levy a local income tax this calculator does not withhold.
    [InlineData(UsState.IN, "county income tax is not withheld")]
    [InlineData(UsState.NY, "Yonkers")]
    [InlineData(UsState.PA, "Philadelphia Wage Tax")]
    [InlineData(UsState.OH, "Municipal and school-district")]
    [InlineData(UsState.MO, "Kansas City and St. Louis")]
    [InlineData(UsState.MI, "City income taxes")]
    [InlineData(UsState.KY, "occupational and payroll taxes")]
    [InlineData(UsState.AL, "occupational taxes")]
    // States with an employee-paid leave program that is not withheld.
    [InlineData(UsState.WA, "Paid Family & Medical Leave employee premium is not withheld")]
    [InlineData(UsState.MN, "Minnesota Paid Leave employee premiums")]
    [InlineData(UsState.ME, "Maine Paid Family and Medical Leave")]
    [InlineData(UsState.DE, "Delaware Paid Leave")]
    public void StandardResult_DisclosesWhatTheStateDoesNotWithhold(UsState state, string expected)
    {
        var notes = NotesFor(state);

        Assert.Contains(notes, note => note.Description.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Maryland_NoLongerClaimsToDivergeFromThePublishedTables()
    {
        // The calculator used to annualize wages and apply the pre-2025 variable
        // 15% standard deduction, and said so in a note. It now follows the
        // guide's per-period percentage method, so the note must be gone rather
        // than left warning about a difference that no longer exists.
        var notes = NotesFor(UsState.MD);

        Assert.DoesNotContain(notes, note =>
            note.Description.Contains("15% standard deduction", StringComparison.OrdinalIgnoreCase)
            || note.Description.Contains("differ modestly from the published tables", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Maryland_StillDisclosesTheDelawareCommutersTableItDoesNotImplement()
    {
        var notes = NotesFor(UsState.MD);

        Assert.Contains(notes, note => note.Description.Contains("work in Delaware", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Maryland_NoLongerClaimsCountyRatesAreBuiltIn()
    {
        // The old text said county rates were "built into Maryland withholding
        // methods" — true of the Comptroller's tables, but the opposite of what
        // this calculator did at the time, and it is what users actually read.
        var notes = NotesFor(UsState.MD);

        Assert.DoesNotContain(notes, note => note.Description.Contains("built into Maryland", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FederalWithholding_DisclosesUnsupportedW4Statuses()
    {
        var notes = NotesFor(UsState.TX);

        Assert.Contains(notes, note => note.Description.Contains("Nonresident-alien", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(notes, note => note.Description.Contains("exempt status", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(UsState.CA)]
    [InlineData(UsState.NJ)]
    [InlineData(UsState.NY)]
    [InlineData(UsState.MA)]
    [InlineData(UsState.RI)]
    [InlineData(UsState.OR)]
    [InlineData(UsState.HI)]
    [InlineData(UsState.CO)]
    [InlineData(UsState.CT)]
    [InlineData(UsState.WA)]
    public void BonusMode_NamesTheProgramItDoesNotWithhold(UsState state)
    {
        using var provider = CreateServices();
        var catalog = provider.GetRequiredService<TaxSourceCatalog>();

        var rule = catalog.Rules.Single(r =>
            r.Scope == TaxRuleScope.SupplementalWithholding && r.AppliesTo(state));

        // Generic boilerplate would leave the user guessing which premium applies.
        Assert.DoesNotContain(rule.Exclusions, e =>
            e == "Local taxes and employee payroll assessments are not included in bonus mode.");
        Assert.Contains(rule.Exclusions, e => e.Contains("bonus mode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryStateThatWithholdsAnAssessment_DisclosesTheBonusModeExclusion()
    {
        using var provider = CreateServices();
        var registry = provider.GetRequiredService<StateCalculatorRegistry>();
        var catalog = provider.GetRequiredService<TaxSourceCatalog>();
        var schemas = provider.GetRequiredService<IStateSchemaProvider>();

        var undisclosed = new List<string>();
        foreach (var state in registry.SupportedStates)
        {
            var values = PayCalculatorTestHarness.DefaultValues(schemas.GetSchema(state));
            var context = new CommonWithholdingContext(
                state, GrossWages: 5_000m, PayPeriod: PayFrequency.Biweekly, Year: 2026);

            if (registry.GetCalculator(state).Calculate(context, values).DisabilityInsurance <= 0m)
                continue;

            var rule = catalog.Rules.Single(r =>
                r.Scope == TaxRuleScope.SupplementalWithholding && r.AppliesTo(state));
            if (!rule.Exclusions.Any(e => e.Contains("bonus mode", StringComparison.OrdinalIgnoreCase)))
                undisclosed.Add(state.ToString());
        }

        Assert.Empty(undisclosed);
    }

    private static IReadOnlyList<AccuracyNote> NotesFor(UsState state)
        => PayCalculatorTestHarness.Calculate(state, grossWages: 3_000m).Explanation.AccuracyNotes;

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddPaycheckCalculatorCore(new DisclosureTaxDataReader());
        return services.BuildServiceProvider();
    }

    private sealed class DisclosureTaxDataReader : ITaxDataReader
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
