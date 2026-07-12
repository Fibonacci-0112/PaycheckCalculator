using System.Globalization;

namespace PaycheckCalculator.App.Behaviors;

public class DecimalFormatBehavior : Behavior<Entry>
{
    private Entry? _entry;

    public static readonly BindableProperty IsCurrencyProperty =
        BindableProperty.Create(nameof(IsCurrency), typeof(bool), typeof(DecimalFormatBehavior), false,
            propertyChanged: OnIsCurrencyChanged);

    public bool IsCurrency
    {
        get => (bool)GetValue(IsCurrencyProperty);
        set => SetValue(IsCurrencyProperty, value);
    }

    public static readonly BindableProperty IsPercentageProperty =
        BindableProperty.Create(nameof(IsPercentage), typeof(bool), typeof(DecimalFormatBehavior), false,
            propertyChanged: OnIsPercentageChanged);

    public bool IsPercentage
    {
        get => (bool)GetValue(IsPercentageProperty);
        set => SetValue(IsPercentageProperty, value);
    }

    protected override void OnAttachedTo(Entry entry)
    {
        _entry = entry;
        entry.Unfocused += OnUnfocused;
        entry.Focused += OnFocused;
        entry.BindingContextChanged += OnEntryBindingContextChanged;
        BindingContext = entry.BindingContext;
        base.OnAttachedTo(entry);
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.Unfocused -= OnUnfocused;
        entry.Focused -= OnFocused;
        entry.BindingContextChanged -= OnEntryBindingContextChanged;
        _entry = null;
        base.OnDetachingFrom(entry);
    }

    private void OnEntryBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is Entry entry)
            BindingContext = entry.BindingContext;
    }

    private static void OnIsCurrencyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DecimalFormatBehavior behavior && behavior._entry is { IsFocused: false } entry)
        {
            behavior.FormatText(entry);
        }
    }

    private static void OnIsPercentageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DecimalFormatBehavior behavior && behavior._entry is { IsFocused: false } entry)
        {
            behavior.FormatText(entry);
        }
    }

    private void OnFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || (!IsCurrency && !IsPercentage))
            return;

        var text = StripPercentSign(entry.Text);
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value))
        {
            entry.Text = value.ToString("0.00", CultureInfo.CurrentCulture);
        }
    }

    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;

        FormatText(entry);
    }

    private void FormatText(Entry entry)
    {
        var text = StripPercentSign(entry.Text);
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var value))
        {
            if (IsPercentage)
                entry.Text = value.ToString("0.00", CultureInfo.CurrentCulture) + "%";
            else if (IsCurrency)
                entry.Text = value.ToString("C2", CultureInfo.CurrentCulture);
            else
                entry.Text = value.ToString("0.00", CultureInfo.CurrentCulture);
        }
    }

    private static string StripPercentSign(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text.Replace("%", "").Trim();
    }
}

