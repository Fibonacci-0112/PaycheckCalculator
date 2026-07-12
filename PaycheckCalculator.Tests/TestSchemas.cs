using System.Collections.Generic;
using System.IO;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Test-only access to the JSON state schema provider, plus an extension
/// method that lets pre-existing tests keep their <c>calc.GetInputSchema()</c>
/// call shape after the interface method was removed in favour of JSON-driven
/// schemas. The schemas are loaded once from the copied
/// <c>Schemas/&lt;state&gt;.json</c> files in the test bin output.
/// </summary>
public static class TestSchemas
{
    public static IStateSchemaProvider Provider { get; } = BuildProvider();

    private static IStateSchemaProvider BuildProvider()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Schemas");
        var map = new Dictionary<UsState, string>();
        if (Directory.Exists(dir))
        {
            foreach (var path in Directory.GetFiles(dir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                if (Enum.TryParse<UsState>(name, out var state))
                    map[state] = File.ReadAllText(path);
            }
        }
        return new JsonStateSchemaProvider(map);
    }
}

/// <summary>
/// Test-only extension that bridges the legacy <c>calc.GetInputSchema()</c>
/// call sites to the new <see cref="IStateSchemaProvider"/>. Keeps existing
/// schema-shape assertions working without touching every test file.
/// </summary>
public static class StateCalculatorSchemaTestExtensions
{
    public static IReadOnlyList<StateFieldDefinition> GetInputSchema(this IStateWithholdingCalculator calc)
        => TestSchemas.Provider.GetSchema(calc.State);
}

