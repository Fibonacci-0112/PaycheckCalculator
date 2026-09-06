using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Shared.Snapshots;

/// <summary>
/// One state-level tax line inside a saved snapshot. Mirrors
/// <see cref="StateTaxLine"/> minus the explanation steps, which are regenerated
/// on demand rather than stored.
/// </summary>
public sealed record SavedStateTaxLineDto
{
    public StateTaxLineKind Kind { get; init; }
    public string Label { get; init; } = "";
    public decimal Amount { get; init; }
    public string? ShortCode { get; init; }
}
