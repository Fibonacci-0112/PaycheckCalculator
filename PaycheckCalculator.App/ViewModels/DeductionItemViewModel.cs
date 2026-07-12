using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalculator.App.Helpers;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.App.ViewModels;

/// <summary>
/// ViewModel for a single deduction item in the deductions collection.
/// The UI binds to these properties to render editable deduction rows.
/// </summary>
public partial class DeductionItemViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial bool HasNameError { get; set; }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            HasNameError = false;
    }
    [ObservableProperty] public partial decimal Amount { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPercentageAmount))]
    [NotifyPropertyChangedFor(nameof(IsDollarAmount))]
    public partial DeductionAmountType AmountType { get; set; } = DeductionAmountType.Dollar;

    [ObservableProperty] public partial bool ReducesFederalTaxableWages { get; set; } = true;
    [ObservableProperty] public partial bool ReducesStateTaxableWages { get; set; } = true;
    [ObservableProperty] public partial bool ReducesFicaWages { get; set; } = true;

    /// <summary>
    /// True when <see cref="AmountType"/> is <see cref="DeductionAmountType.Percentage"/>.
    /// Used by the UI to switch the amount entry format between currency and percent.
    /// </summary>
    public bool IsPercentageAmount => AmountType == DeductionAmountType.Percentage;

    /// <summary>
    /// True when <see cref="AmountType"/> is not <see cref="DeductionAmountType.Percentage"/>.
    /// Used by the UI to apply currency formatting to the amount entry.
    /// </summary>
    public bool IsDollarAmount => AmountType != DeductionAmountType.Percentage;

    /// <summary>
    /// The deduction type (PreTax/PostTax) as a plain enum value, used by mappers and view model logic.
    /// </summary>
    public DeductionType Type => SelectedDeductionTypePickerItem?.Value ?? DeductionType.PreTax;

    /// <summary>
    /// Picker items for deduction type with user-friendly display labels (e.g. "Pre-Tax", "Post-Tax").
    /// </summary>
    public IReadOnlyList<PickerItem<DeductionType>> DeductionTypeItems { get; } =
        Enum.GetValues<DeductionType>()
            .Select(t => new PickerItem<DeductionType>(t, EnumDisplay.DeductionType(t.ToString())))
            .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Type))]
    public partial PickerItem<DeductionType>? SelectedDeductionTypePickerItem { get; set; }

    public DeductionItemViewModel()
    {
        SelectedDeductionTypePickerItem = DeductionTypeItems[0];
    }

    public IReadOnlyList<DeductionAmountType> AmountTypes { get; } =
        Enum.GetValues<DeductionAmountType>().ToList();

    /// <summary>
    /// Maps this view model to a core <see cref="Deduction"/> domain object.
    /// </summary>
    public Deduction ToDeduction() => new()
    {
        Name = Name,
        Type = Type,
        Amount = Amount,
        AmountType = AmountType,
        ReducesFederalTaxableWages = ReducesFederalTaxableWages,
        ReducesStateTaxableWages = ReducesStateTaxableWages,
        ReducesFicaWages = ReducesFicaWages
    };
}
