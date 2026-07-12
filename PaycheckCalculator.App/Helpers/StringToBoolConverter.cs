using System.Globalization;

namespace PaycheckCalculator.App.Helpers;

/// <summary>
/// Returns <c>true</c> when the bound string is non-empty. Used to hide
/// optional text rows (e.g. a transaction description) when blank.
/// </summary>
public sealed class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
