using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Shared.Snapshots;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="SavedPaycheckMerger"/> — the deterministic last-write-wins merge that runs
/// server-side and resolves conflicts between two saved sets by paycheck name (case-insensitive).
/// </summary>
public sealed class SavedPaycheckMergerTest
{
    private static readonly DateTimeOffset Earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static SavedPaycheckDto Entry(string name, DateTimeOffset updatedAt, decimal netPay = 100m) => new()
    {
        Name = name,
        UpdatedAtUtc = updatedAt,
        Input = new PaycheckInput { State = UsState.TX },
        Result = new SavedPaycheckResultDto { NetPay = netPay }
    };

    private static SavedPaycheckSet Set(
        IEnumerable<SavedPaycheckDto>? entries = null,
        IEnumerable<SavedPaycheckTombstone>? tombstones = null)
        => new(
            (entries ?? Array.Empty<SavedPaycheckDto>()).ToList(),
            (tombstones ?? Array.Empty<SavedPaycheckTombstone>()).ToList());

    [Fact]
    public void NewerEntry_Wins()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("Job", Earlier, netPay: 1m) }),
            Set(new[] { Entry("Job", Later, netPay: 2m) }));

        Assert.Single(merged.Paychecks);
        Assert.Equal(2m, merged.Paychecks[0].Result.NetPay);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void NewerTombstone_DeletesEntry()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("Job", Earlier) }),
            Set(tombstones: new[] { new SavedPaycheckTombstone("Job", Later) }));

        Assert.Empty(merged.Paychecks);
        Assert.Single(merged.Tombstones);
    }

    [Fact]
    public void NewerEntry_BeatsOlderTombstone_Resurrects()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(tombstones: new[] { new SavedPaycheckTombstone("Job", Earlier) }),
            Set(new[] { Entry("Job", Later) }));

        Assert.Single(merged.Paychecks);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void OlderEntry_DoesNotResurrect_NewerTombstone()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("Job", Earlier) }),
            Set(tombstones: new[] { new SavedPaycheckTombstone("Job", Later) }));

        Assert.Empty(merged.Paychecks);
        Assert.Single(merged.Tombstones);
    }

    [Fact]
    public void Tie_EntryBeatsTombstone()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(tombstones: new[] { new SavedPaycheckTombstone("Job", Earlier) }),
            Set(new[] { Entry("Job", Earlier) }));

        Assert.Single(merged.Paychecks);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public void EntryEntryTie_IncomingWins()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("Job", Earlier, netPay: 1m) }),
            Set(new[] { Entry("Job", Earlier, netPay: 2m) }));

        Assert.Single(merged.Paychecks);
        Assert.Equal(2m, merged.Paychecks[0].Result.NetPay);
    }

    [Fact]
    public void CaseInsensitiveName_IsSameKey_WinnerCasingKept()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("job a", Earlier) }),
            Set(new[] { Entry("JOB A", Later) }));

        Assert.Single(merged.Paychecks);
        Assert.Equal("JOB A", merged.Paychecks[0].Name);
    }

    [Fact]
    public void DisjointNames_AreUnioned()
    {
        var merged = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("A", Earlier) }),
            Set(new[] { Entry("B", Earlier) }));

        Assert.Equal(2, merged.Paychecks.Count);
        Assert.Contains(merged.Paychecks, p => p.Name == "A");
        Assert.Contains(merged.Paychecks, p => p.Name == "B");
    }

    [Fact]
    public void Merge_IsIdempotent_WhenReapplyingMergedResult()
    {
        var first = SavedPaycheckMerger.Merge(
            Set(new[] { Entry("A", Earlier) }),
            Set(new[] { Entry("B", Later) }, new[] { new SavedPaycheckTombstone("C", Later) }));

        var second = SavedPaycheckMerger.Merge(first, first);

        Assert.Equal(first.Paychecks.Count, second.Paychecks.Count);
        Assert.Equal(first.Tombstones.Count, second.Tombstones.Count);
    }
}
