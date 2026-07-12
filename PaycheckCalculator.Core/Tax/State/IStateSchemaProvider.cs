using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Supplies the declarative UI input schema for each state. Schemas are loaded
/// from JSON files in <c>PaycheckCalculator.Core/Data/Schemas/</c>, decoupling the
/// state input definitions from calculator code so year-over-year UI changes
/// can be made without touching C#.
/// </summary>
public interface IStateSchemaProvider
{
    /// <summary>
    /// Returns the input field definitions for the given state. Returns an
    /// empty list when the state has no schema (e.g. no-income-tax states).
    /// </summary>
    IReadOnlyList<StateFieldDefinition> GetSchema(UsState state);

    /// <summary>
    /// Returns the option strings for a Picker field in the given state's
    /// schema, or an empty list when the field is missing or non-Picker.
    /// Used by per-state <c>Validate()</c> implementations to check user input
    /// against the JSON-defined option set (single source of truth).
    /// </summary>
    IReadOnlyList<string> GetOptions(UsState state, string fieldKey);
}
