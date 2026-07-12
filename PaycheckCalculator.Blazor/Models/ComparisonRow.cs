using System.Globalization;

namespace PaycheckCalculator.Blazor.Models;

/// <summary>
/// One metric compared across two saved paychecks (value A vs value B), with the
/// B − A difference. Used by the calculator page's side-by-side comparison table.
/// Mirrors the MAUI app's <c>ComparisonRow</c>.
/// </summary>
public sealed class ComparisonRow
{
    public ComparisonRow(string label, decimal valueA, decimal valueB, bool highlight = false)
    {
        Label = label;
        ValueA = valueA;
        ValueB = valueB;
        Highlight = highlight;
    }

    public string Label { get; }
    public decimal ValueA { get; }
    public decimal ValueB { get; }

    /// <summary>True for the headline row (Net Pay) so the UI can emphasize it.</summary>
    public bool Highlight { get; }

    /// <summary>Difference of paycheck B relative to paycheck A.</summary>
    public decimal Difference => ValueB - ValueA;

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public string ValueADisplay => ValueA.ToString("C", Us);
    public string ValueBDisplay => ValueB.ToString("C", Us);

    /// <summary>Signed currency difference, e.g. "+$120.00" or "−$45.00" (U+2212 minus).</summary>
    public string DifferenceDisplay =>
        (Difference >= 0 ? "+" : "−") + Math.Abs(Difference).ToString("C", Us);
}
