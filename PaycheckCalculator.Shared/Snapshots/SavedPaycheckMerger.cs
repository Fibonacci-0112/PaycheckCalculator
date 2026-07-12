namespace PaycheckCalculator.Shared.Snapshots;

/// <summary>
/// Deterministic last-write-wins merge of two <see cref="SavedPaycheckSet"/>s, keyed by paycheck name
/// (case-insensitive). The same logic runs on the server (merging a client's push with the stored
/// state) and is available to clients/tests, so every party resolves conflicts identically.
/// <para>Per name key, the winner is decided by:</para>
/// <list type="number">
///   <item>the later timestamp wins;</item>
///   <item>on an exact timestamp tie, a live entry beats a tombstone (deletes never win ties);</item>
///   <item>on a tie between two of the same kind, the <c>incoming</c> side wins (idempotent re-sync).</item>
/// </list>
/// The winning record keeps its own <c>Name</c> casing.
/// </summary>
public static class SavedPaycheckMerger
{
    public static SavedPaycheckSet Merge(SavedPaycheckSet existing, SavedPaycheckSet incoming)
    {
        var winners = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

        void Consider(Candidate candidate)
        {
            if (!winners.TryGetValue(candidate.Key, out var current) || candidate.Beats(current))
                winners[candidate.Key] = candidate;
        }

        foreach (var entry in existing.Paychecks) Consider(Candidate.ForEntry(entry, fromIncoming: false));
        foreach (var tomb in existing.Tombstones) Consider(Candidate.ForTombstone(tomb, fromIncoming: false));
        foreach (var entry in incoming.Paychecks) Consider(Candidate.ForEntry(entry, fromIncoming: true));
        foreach (var tomb in incoming.Tombstones) Consider(Candidate.ForTombstone(tomb, fromIncoming: true));

        var paychecks = new List<SavedPaycheckDto>();
        var tombstones = new List<SavedPaycheckTombstone>();
        foreach (var winner in winners.Values)
        {
            if (winner.Entry is not null) paychecks.Add(winner.Entry);
            else tombstones.Add(winner.Tombstone!);
        }

        // Stable, name-ordered output so re-syncs and tests are deterministic.
        paychecks.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        tombstones.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new SavedPaycheckSet(paychecks, tombstones);
    }

    private sealed class Candidate
    {
        public required string Key { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public bool IsTombstone { get; init; }
        public bool FromIncoming { get; init; }
        public SavedPaycheckDto? Entry { get; init; }
        public SavedPaycheckTombstone? Tombstone { get; init; }

        public static Candidate ForEntry(SavedPaycheckDto dto, bool fromIncoming) => new()
        {
            Key = dto.Name,
            Timestamp = dto.UpdatedAtUtc,
            IsTombstone = false,
            FromIncoming = fromIncoming,
            Entry = dto
        };

        public static Candidate ForTombstone(SavedPaycheckTombstone tomb, bool fromIncoming) => new()
        {
            Key = tomb.Name,
            Timestamp = tomb.DeletedAtUtc,
            IsTombstone = true,
            FromIncoming = fromIncoming,
            Tombstone = tomb
        };

        /// <summary>True when this candidate should replace <paramref name="other"/>.</summary>
        public bool Beats(Candidate other)
        {
            if (Timestamp != other.Timestamp)
                return Timestamp > other.Timestamp;
            if (IsTombstone != other.IsTombstone)
                return !IsTombstone; // a live entry beats a tombstone on a tie
            return FromIncoming && !other.FromIncoming; // same kind + same time: incoming wins
        }
    }
}
