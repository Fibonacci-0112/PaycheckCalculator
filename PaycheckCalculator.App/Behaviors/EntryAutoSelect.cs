using Microsoft.Maui.Handlers;

namespace PaycheckCalculator.App.Behaviors;

/// <summary>
/// Highlights an <see cref="Entry"/>'s whole value as soon as the field takes focus, so moving
/// from input to input (tab, arrow key, or tap) leaves the field ready to be typed over instead
/// of making the user clear the previous value first.
/// </summary>
/// <remarks>
/// Installed once from <c>MauiProgram</c> through the <see cref="EntryHandler"/> mapper so it
/// covers every Entry in the app, including the schema-driven state fields and the budget rows
/// that are created at runtime. A single Entry can opt out in XAML with
/// <c>behaviors:EntryAutoSelect.IsEnabled="False"</c>.
/// </remarks>
public static class EntryAutoSelect
{
    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.CreateAttached(
            "IsEnabled",
            typeof(bool),
            typeof(EntryAutoSelect),
            defaultValue: true);

    public static bool GetIsEnabled(BindableObject view) => (bool)view.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(BindableObject view, bool value) => view.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// Hooks the Entry handler mapper. Safe to call once at startup; the mapping runs when each
    /// Entry's handler is connected.
    /// </summary>
    public static void Install()
    {
        EntryHandler.Mapper.AppendToMapping(nameof(EntryAutoSelect), (_, view) =>
        {
            if (view is not Entry entry)
                return;

            // Handlers can be reconnected (for example when a page is re-created), so drop any
            // previous subscription before adding one.
            entry.Focused -= OnEntryFocused;
            entry.Focused += OnEntryFocused;
        });
    }

    private static void OnEntryFocused(object? sender, FocusEventArgs e)
    {
        // Password fields are left alone: re-focusing one is usually a correction, not a retype.
        if (sender is not Entry entry || !e.IsFocused || entry.IsPassword || !GetIsEnabled(entry))
            return;

        // Deferred to the next dispatcher turn for two reasons: DecimalFormatBehavior rewrites the
        // text on focus (so the final length is only known afterwards), and the platform places the
        // caret from the tap after the Focused event has been raised.
        entry.Dispatcher.Dispatch(() =>
        {
            if (!entry.IsFocused)
                return;

            var length = entry.Text?.Length ?? 0;
            if (length == 0)
                return;

            entry.CursorPosition = 0;
            entry.SelectionLength = length;
        });
    }
}
