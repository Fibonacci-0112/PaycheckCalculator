using System.Globalization;

namespace PaycheckCalc.App.Helpers;

/// <summary>
/// Returns the logical negation of a bound <see cref="bool"/>. Used to drive
/// "empty state" visuals (e.g. show a hint only when there is no income yet).
/// </summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value is null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
