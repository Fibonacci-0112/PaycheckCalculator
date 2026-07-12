using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.App.ViewModels;

/// <summary>
/// ViewModel for a single dynamic state input field.
/// The UI binds to these properties to render the appropriate control
/// (Picker, Entry, Switch) and read/write the current value.
/// Performs local parsing and required-field validation, exposing error state.
/// </summary>
public partial class StateFieldViewModel : ObservableObject
{
    public StateFieldViewModel(StateFieldDefinition definition)
    {
        Definition = definition;

        // Initialize from the schema default
        if (definition.FieldType == StateFieldType.Picker)
            SelectedOption = definition.DefaultValue?.ToString()
                ?? definition.Options?.FirstOrDefault() ?? "";
        else if (definition.FieldType == StateFieldType.Toggle)
            BoolValue = definition.DefaultValue is true;
        else if (definition.FieldType == StateFieldType.Text)
            StringValue = definition.DefaultValue?.ToString() ?? "";
        else
            StringValue = definition.DefaultValue?.ToString() ?? "0";
    }

    public StateFieldDefinition Definition { get; }

    public string Key => Definition.Key;
    public string Label => Definition.Label;
    public IReadOnlyList<string>? Options => Definition.Options;

    // Visibility flags for XAML control switching
    public bool IsPicker => Definition.FieldType == StateFieldType.Picker;
    public bool IsText => Definition.FieldType == StateFieldType.Text;
    public bool IsNumeric => Definition.FieldType is StateFieldType.Integer or StateFieldType.Decimal;
    public bool IsToggle => Definition.FieldType == StateFieldType.Toggle;
    public bool IsCurrency => Definition.FieldType == StateFieldType.Decimal;

    // Picker value
    [ObservableProperty] public partial string? SelectedOption { get; set; }

    // Text/Numeric value (Entry binding)
    [ObservableProperty] public partial string StringValue { get; set; } = "0";

    // Toggle value (Switch binding)
    [ObservableProperty] public partial bool BoolValue { get; set; }

    // Error state
    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnSelectedOptionChanged(string? value) => Validate();
    partial void OnStringValueChanged(string value) => Validate();
    partial void OnBoolValueChanged(bool value) => Validate();

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    /// <summary>
    /// Performs local parsing and required-field validation.
    /// Sets <see cref="ErrorMessage"/> accordingly.
    /// </summary>
    public void Validate()
    {
        ErrorMessage = Definition.FieldType switch
        {
            StateFieldType.Picker => ValidatePicker(),
            StateFieldType.Integer => ValidateInteger(),
            StateFieldType.Decimal => ValidateDecimal(),
            StateFieldType.Text => ValidateText(),
            StateFieldType.Toggle => null, // toggles always have a valid bool value
            _ => null
        };
    }

    private string? ValidatePicker()
    {
        // When a Picker is inside a BindableLayout DataTemplate, MAUI may
        // temporarily set SelectedItem to null during binding initialization.
        // Use the schema default when SelectedOption is null so that the
        // default selection doesn't block the calculation with a spurious error.
        var effective = SelectedOption
            ?? Definition.DefaultValue?.ToString()
            ?? Definition.Options?.FirstOrDefault();
        if (Definition.IsRequired && string.IsNullOrWhiteSpace(effective))
            return $"{Label} is required.";
        return null;
    }

    private string? ValidateInteger()
    {
        if (Definition.IsRequired && string.IsNullOrWhiteSpace(StringValue))
            return $"{Label} is required.";
        if (!string.IsNullOrWhiteSpace(StringValue) && !int.TryParse(StringValue, out _))
            return $"{Label} must be a whole number.";
        return null;
    }

    private string? ValidateDecimal()
    {
        if (Definition.IsRequired && string.IsNullOrWhiteSpace(StringValue))
            return $"{Label} is required.";
        if (!string.IsNullOrWhiteSpace(StringValue) && !decimal.TryParse(StringValue, out _))
            return $"{Label} must be a valid number.";
        return null;
    }

    private string? ValidateText()
    {
        if (Definition.IsRequired && string.IsNullOrWhiteSpace(StringValue))
            return $"{Label} is required.";
        return null;
    }

    /// <summary>
    /// Returns the resolved typed value to store in <see cref="StateInputValues"/>.
    /// For Picker fields, falls back to the schema default when <see cref="SelectedOption"/>
    /// is null (which can happen when the MAUI Picker resets SelectedItem during
    /// binding initialization inside a BindableLayout DataTemplate).
    /// </summary>
    public object? GetResolvedValue() => Definition.FieldType switch
    {
        StateFieldType.Picker => SelectedOption
            ?? Definition.DefaultValue?.ToString()
            ?? Definition.Options?.FirstOrDefault(),
        StateFieldType.Toggle => BoolValue,
        StateFieldType.Integer => int.TryParse(StringValue, out var i) ? i : 0,
        StateFieldType.Decimal => decimal.TryParse(StringValue, out var d) ? d : 0m,
        StateFieldType.Text => StringValue,
        _ => StringValue
    };
}
