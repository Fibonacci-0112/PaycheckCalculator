namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Describes the type of a dynamic state input field for UI rendering.
/// </summary>
public enum StateFieldType
{
    /// <summary>Free-text input.</summary>
    Text,
    /// <summary>Whole-number input (e.g., allowances, dependents).</summary>
    Integer,
    /// <summary>Decimal/currency input (e.g., additional withholding).</summary>
    Decimal,
    /// <summary>Boolean toggle (e.g., exempt flag).</summary>
    Toggle,
    /// <summary>Pick-list with predefined options (e.g., filing status).</summary>
    Picker
}

/// <summary>
/// Metadata that describes a single input field required by a state calculator.
/// Drives dynamic UI rendering: the UI reads these definitions and builds
/// the appropriate controls (picker, entry, switch, etc.) at runtime.
/// </summary>
public sealed class StateFieldDefinition
{
    /// <summary>
    /// Machine-readable key used to store/retrieve the value in
    /// <see cref="StateInputValues"/> (e.g. "FilingStatus", "Allowances").
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Human-readable label shown next to the control.</summary>
    public required string Label { get; init; }

    /// <summary>Control type the UI should render.</summary>
    public required StateFieldType FieldType { get; init; }

    /// <summary>Whether the field must be filled before calculation.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Default value pre-populated when the state is first selected.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Display labels for <see cref="StateFieldType.Picker"/> fields.
    /// Each string is both the display text and the value stored in
    /// <see cref="StateInputValues"/>.
    /// Null or empty for non-picker fields.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }
}
