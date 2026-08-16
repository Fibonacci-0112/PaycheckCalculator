using Microsoft.AspNetCore.Components;

namespace PaycheckCalculator.Blazor.Components.Layout;

/// <summary>
/// Per-circuit chrome state shared between <see cref="MainLayout"/> and the page it hosts.
/// <para>
/// The layout owns the sidebar and top bar, but the title, the top-bar action button and the
/// saved-paycheck badge count are all page concerns. The page pushes them here and the layout
/// re-renders, which keeps the chrome out of every page's markup without the layout having to
/// know anything about the calculator.
/// </para>
/// </summary>
public sealed class ShellState
{
    /// <summary>Title shown in the top bar. Pages set this in <c>OnInitialized</c>.</summary>
    public string Title { get; private set; } = "Paycheck Calculator";

    /// <summary>Optional top-bar action content, rendered right-aligned.</summary>
    public RenderFragment? Actions { get; private set; }

    /// <summary>Saved-paycheck count shown as a badge on the sidebar's Saved Paychecks entry.</summary>
    public int SavedCount { get; private set; }

    /// <summary>Raised whenever any of the above changes, so the layout can re-render.</summary>
    public event Action? Changed;

    /// <summary>Sets the top-bar title and (optionally) its action content.</summary>
    public void Set(string title, RenderFragment? actions = null)
    {
        Title = title;
        Actions = actions;
        Changed?.Invoke();
    }

    /// <summary>Updates the sidebar badge; no-ops when the count is unchanged.</summary>
    public void SetSavedCount(int count)
    {
        if (SavedCount == count) return;
        SavedCount = count;
        Changed?.Invoke();
    }

    /// <summary>
    /// Re-renders the chrome without changing anything. Pages call this when state captured by
    /// their <see cref="Actions"/> fragment has changed (for example, a result becoming available
    /// enables the export button).
    /// </summary>
    public void Refresh() => Changed?.Invoke();
}
