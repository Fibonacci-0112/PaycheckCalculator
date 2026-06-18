using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.TaxYears;

namespace PaycheckCalc.Shared.Snapshots;

/// <summary>
/// A single saved paycheck, identified by its user-facing <see cref="Name"/> (case-insensitive).
/// Carries both the full domain <see cref="PaycheckInput"/> that produced it and the computed
/// <see cref="Result"/> numbers, so it can be synced, restored for display, and (in a future change)
/// reloaded into the calculator form.
/// </summary>
public sealed record SavedPaycheckDto
{
    /// <summary>User-facing label and the logical identity used for upsert/merge (case-insensitive).</summary>
    public required string Name { get; init; }

    /// <summary>When this snapshot was last written. Drives last-write-wins merge.</summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Snapshot schema version, for forward-compatible migrations.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Tax year used to produce this saved calculation. Defaults migrate legacy snapshots.</summary>
    public int TaxYear { get; init; } = FixedTaxYearProvider.BundledTaxYear;

    /// <summary>The exact input the calculator ran.</summary>
    public required PaycheckInput Input { get; init; }

    /// <summary>The computed result numbers for display.</summary>
    public required SavedPaycheckResultDto Result { get; init; }
}
