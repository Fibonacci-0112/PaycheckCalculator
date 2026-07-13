using Microsoft.AspNetCore.Components;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Blazor.Components.Pages;

public partial class Calculator
{
    /// <summary>
    /// When set by a state landing page, the calculator pre-selects this state on load.
    /// </summary>
    [Parameter] public UsState? InitialState { get; set; }

    internal sealed class StateFieldVm
    {
        public StateFieldDefinition Def { get; }
        public string Key => Def.Key;
        public string Label => Def.Label;
        public StateFieldType FieldType => Def.FieldType;
        public IReadOnlyList<string>? Options => Def.Options;

        public string? SelectedOption { get; set; }
        public string StringValue { get; set; } = "";
        public bool BoolValue { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public StateFieldVm(StateFieldDefinition def)
        {
            Def = def;
            switch (def.FieldType)
            {
                case StateFieldType.Picker:
                    SelectedOption = def.DefaultValue?.ToString() ?? def.Options?.FirstOrDefault();
                    break;
                case StateFieldType.Toggle:
                    BoolValue = def.DefaultValue is bool b ? b
                        : def.DefaultValue != null && Convert.ToBoolean(def.DefaultValue);
                    break;
                default:
                    StringValue = def.DefaultValue?.ToString() ?? "0";
                    break;
            }
        }

        public void RestoreFrom(StateFieldVm old)
        {
            SelectedOption = old.SelectedOption;
            StringValue = old.StringValue;
            BoolValue = old.BoolValue;
        }

        public void Validate()
        {
            ErrorMessage = null;
            if (Def.IsRequired)
            {
                if (Def.FieldType == StateFieldType.Picker && string.IsNullOrEmpty(SelectedOption))
                    ErrorMessage = $"{Label} is required.";
                else if (Def.FieldType == StateFieldType.Text && string.IsNullOrWhiteSpace(StringValue))
                    ErrorMessage = $"{Label} is required.";
            }
            if (Def.FieldType == StateFieldType.Integer
                && !string.IsNullOrEmpty(StringValue)
                && !int.TryParse(StringValue, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                ErrorMessage = $"{Label} must be a whole number.";
            if (Def.FieldType == StateFieldType.Decimal
                && !string.IsNullOrEmpty(StringValue)
                && !decimal.TryParse(StringValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                ErrorMessage = $"{Label} must be a number.";
        }

        public object? GetValue() => Def.FieldType switch
        {
            StateFieldType.Picker => (object?)(SelectedOption ?? Def.DefaultValue?.ToString()),
            StateFieldType.Toggle => BoolValue,
            StateFieldType.Integer => int.TryParse(StringValue, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : 0,
            StateFieldType.Decimal => decimal.TryParse(StringValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m,
            _ => StringValue
        };
    }

    private sealed class DeductionVm
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public bool NameError { get; set; }
        public decimal Amount { get; set; } = 0m;
        public DeductionType Type { get; set; } = DeductionType.PreTax;
        public DeductionAmountType AmountType { get; set; } = DeductionAmountType.Dollar;
        public bool ReducesFederalTaxableWages { get; set; } = true;
        public bool ReducesStateTaxableWages { get; set; } = true;
        public bool ReducesFicaWages { get; set; } = true;

        public Deduction ToDeduction() => new()
        {
            Name = Name,
            Amount = Amount,
            Type = Type,
            AmountType = AmountType,
            ReducesFederalTaxableWages = ReducesFederalTaxableWages,
            ReducesStateTaxableWages = ReducesStateTaxableWages,
            ReducesFicaWages = ReducesFicaWages
        };
    }

}
