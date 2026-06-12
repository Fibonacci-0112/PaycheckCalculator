using PaycheckCalc.Shared.Snapshots;
using PaycheckCalc.Shared.Sync;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// In-memory saved-paycheck store for the Blazor app. Registered as scoped, so its lifetime is the
/// Blazor Server circuit: when the user closes the tab the circuit ends and all data is discarded —
/// satisfying the requirement that anonymous web data persists only until the page is closed. Signing
/// in syncs this session's data to the server, which retains it across sessions.
/// </summary>
public sealed class SessionPaycheckStore : ISavedPaycheckStore
{
    private readonly Dictionary<string, SavedPaycheckDto> _paychecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedPaycheckTombstone> _tombstones = new(StringComparer.OrdinalIgnoreCase);

    public Task<SavedPaycheckSet> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(new SavedPaycheckSet(_paychecks.Values.ToList(), _tombstones.Values.ToList()));

    public Task UpsertAsync(SavedPaycheckDto dto, CancellationToken ct = default)
    {
        _paychecks[dto.Name] = dto;
        _tombstones.Remove(dto.Name);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        _paychecks.Remove(name);
        _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        return Task.CompletedTask;
    }

    public Task ClearAsync(DateTimeOffset deletedAtUtc, CancellationToken ct = default)
    {
        foreach (var name in _paychecks.Keys.ToList())
            _tombstones[name] = new SavedPaycheckTombstone(name, deletedAtUtc);
        _paychecks.Clear();
        return Task.CompletedTask;
    }

    public Task ReplaceAllAsync(SavedPaycheckSet set, CancellationToken ct = default)
    {
        _paychecks.Clear();
        _tombstones.Clear();
        foreach (var dto in set.Paychecks) _paychecks[dto.Name] = dto;
        foreach (var tomb in set.Tombstones) _tombstones[tomb.Name] = tomb;
        return Task.CompletedTask;
    }
}
