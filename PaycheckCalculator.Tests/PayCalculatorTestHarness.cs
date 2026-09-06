using System.IO;
using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.DependencyInjection;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Runs a paycheck through the real DI-wired pipeline — the same registry,
/// schemas, rate tables and source manifest the apps load. Tests that care about
/// what the user actually sees (line ordering, aggregates, net pay) go through
/// here rather than calling a state calculator in isolation.
/// </summary>
public static class PayCalculatorTestHarness
{
    /// <summary>
    /// Calculates a fully specified paycheck. Use this when the scenario needs
    /// inputs the convenience overload does not expose — hourly pay, a W-4, or
    /// deductions.
    /// </summary>
    public static PaycheckResult Calculate(PaycheckInput input)
    {
        using var provider = CreateServices();
        return provider.GetRequiredService<PayCalculator>().Calculate(input);
    }

    /// <summary>Calculates a full paycheck for <paramref name="state"/>.</summary>
    public static PaycheckResult Calculate(
        UsState state,
        decimal grossWages,
        PayFrequency frequency = PayFrequency.Biweekly,
        decimal ytdStateWages = 0m,
        StateInputValues? stateValues = null)
    {
        using var provider = CreateServices();
        var calculator = provider.GetRequiredService<PayCalculator>();
        var schemas = provider.GetRequiredService<IStateSchemaProvider>();

        var values = stateValues ?? DefaultValues(schemas.GetSchema(state));

        return calculator.Calculate(new PaycheckInput
        {
            State = state,
            PayType = PayType.Salary,
            SalaryAmount = grossWages,
            SalaryBasis = SalaryBasis.PerPeriod,
            Frequency = frequency,
            YtdStateWages = ytdStateWages,
            StateInputValues = values
        });
    }

    /// <summary>The ordered state tax lines a paycheck produces.</summary>
    public static IReadOnlyList<StateTaxLine> StateLines(
        UsState state,
        decimal grossWages,
        PayFrequency frequency = PayFrequency.Biweekly,
        decimal ytdStateWages = 0m,
        StateInputValues? stateValues = null)
        => Calculate(state, grossWages, frequency, ytdStateWages, stateValues).StateTaxLines;

    /// <summary>Every schema field at its declared default, as the UI would submit them.</summary>
    public static StateInputValues DefaultValues(IReadOnlyList<StateFieldDefinition> schema)
    {
        var values = new StateInputValues();
        foreach (var field in schema)
        {
            values[field.Key] = field.DefaultValue ?? field.FieldType switch
            {
                StateFieldType.Integer => 0,
                StateFieldType.Decimal => 0m,
                StateFieldType.Toggle => false,
                StateFieldType.Picker => field.Options?.FirstOrDefault() ?? "",
                _ => (object)""
            };
        }
        return values;
    }

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
