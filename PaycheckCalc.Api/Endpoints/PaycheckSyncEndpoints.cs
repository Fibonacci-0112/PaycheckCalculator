using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Api.Data;
using PaycheckCalc.Shared.Json;
using PaycheckCalc.Shared.Snapshots;
using PaycheckCalc.Shared.Sync;

namespace PaycheckCalc.Api.Endpoints;

/// <summary>
/// The authorized paycheck sync endpoints. <c>POST /sync</c> merges a client's pushed state with the
/// stored state (server-side, via <see cref="SavedPaycheckMerger"/>) and returns the merged result;
/// <c>GET /</c> returns the stored state without writing.
/// </summary>
public static class PaycheckSyncEndpoints
{
    private const int MaxNameLength = 100;
    private const int MaxEntries = 500;

    public static RouteGroupBuilder MapPaycheckSyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sync", SyncAsync);
        group.MapGet("/", GetAsync);
        return group;
    }

    private static async Task<IResult> SyncAsync(
        SyncRequest request,
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        if (!TryValidate(request, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error] });

        var existing = await LoadSetAsync(db, userId, ct);
        var incoming = new SavedPaycheckSet(request.Paychecks, request.Tombstones);
        var merged = SavedPaycheckMerger.Merge(existing, incoming);

        await PersistAsync(db, userId, merged, ct);

        return Results.Ok(new SyncResponse(merged.Paychecks, merged.Tombstones, DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var set = await LoadSetAsync(db, userId, ct);
        return Results.Ok(new SyncResponse(set.Paychecks, set.Tombstones, DateTimeOffset.UtcNow));
    }

    private static bool TryValidate(SyncRequest request, out string error)
    {
        if (request.Paychecks.Count + request.Tombstones.Count > MaxEntries)
        {
            error = $"Too many entries (max {MaxEntries}).";
            return false;
        }

        foreach (var name in request.Paychecks.Select(p => p.Name).Concat(request.Tombstones.Select(t => t.Name)))
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            {
                error = $"Each paycheck name must be 1–{MaxNameLength} characters.";
                return false;
            }
        }

        error = "";
        return true;
    }

    private static async Task<SavedPaycheckSet> LoadSetAsync(SyncDbContext db, string userId, CancellationToken ct)
    {
        var rows = await db.SavedPaychecks.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);

        var paychecks = new List<SavedPaycheckDto>();
        var tombstones = new List<SavedPaycheckTombstone>();
        foreach (var row in rows)
        {
            if (row.IsDeleted)
            {
                tombstones.Add(new SavedPaycheckTombstone(row.Name, row.UpdatedAtUtc));
            }
            else
            {
                var dto = JsonSerializer.Deserialize<SavedPaycheckDto>(row.PayloadJson, PaycheckJson.Options);
                if (dto is not null) paychecks.Add(dto);
            }
        }

        return new SavedPaycheckSet(paychecks, tombstones);
    }

    private static async Task PersistAsync(SyncDbContext db, string userId, SavedPaycheckSet merged, CancellationToken ct)
    {
        var rows = await db.SavedPaychecks.Where(r => r.UserId == userId).ToListAsync(ct);
        var byKey = rows.ToDictionary(r => r.NameKey, StringComparer.Ordinal);
        var keep = new HashSet<string>(StringComparer.Ordinal);

        void Upsert(string name, DateTimeOffset timestamp, bool isDeleted, string payload)
        {
            var key = name.ToLowerInvariant();
            keep.Add(key);
            if (byKey.TryGetValue(key, out var row))
            {
                row.Name = name;
                row.UpdatedAtUtc = timestamp;
                row.IsDeleted = isDeleted;
                row.PayloadJson = payload;
            }
            else
            {
                db.SavedPaychecks.Add(new SavedPaycheckEntity
                {
                    UserId = userId,
                    NameKey = key,
                    Name = name,
                    UpdatedAtUtc = timestamp,
                    IsDeleted = isDeleted,
                    PayloadJson = payload
                });
            }
        }

        foreach (var entry in merged.Paychecks)
            Upsert(entry.Name, entry.UpdatedAtUtc, isDeleted: false, JsonSerializer.Serialize(entry, PaycheckJson.Options));
        foreach (var tomb in merged.Tombstones)
            Upsert(tomb.Name, tomb.DeletedAtUtc, isDeleted: true, string.Empty);

        // The merge is a union of keys, so this never drops anything today; kept for safety.
        foreach (var row in rows)
            if (!keep.Contains(row.NameKey))
                db.SavedPaychecks.Remove(row);

        await db.SaveChangesAsync(ct);
    }
}
