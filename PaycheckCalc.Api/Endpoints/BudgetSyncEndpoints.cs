using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Api.Data;
using PaycheckCalc.Shared.Budgeting;
using PaycheckCalc.Shared.Json;

namespace PaycheckCalc.Api.Endpoints;

/// <summary>
/// Authorized budget + transaction sync endpoints. <c>POST /sync</c> merges the client's pushed
/// state with the stored state (server-side, via <see cref="BudgetMerger"/>) and returns the merged
/// result; <c>GET /</c> returns the stored state without writing.
/// </summary>
public static class BudgetSyncEndpoints
{
    private const int MaxNameLength = 100;
    private const int MaxEntries = 500;

    public static RouteGroupBuilder MapBudgetSyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sync", SyncAsync);
        group.MapGet("/", GetAsync);
        return group;
    }

    private static async Task<IResult> SyncAsync(
        BudgetSyncRequest request,
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        if (!TryValidate(request, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error] });

        var (existingBudgets, existingTransactions) = await LoadSetsAsync(db, userId, ct);
        var incomingBudgets      = new BudgetSet(request.Budgets, request.BudgetTombstones);
        var incomingTransactions = new TransactionSet(request.Transactions, request.TransactionTombstones);

        var mergedBudgets      = BudgetMerger.MergeBudgets(existingBudgets, incomingBudgets);
        var mergedTransactions = BudgetMerger.MergeTransactions(existingTransactions, incomingTransactions);

        await PersistAsync(db, userId, mergedBudgets, mergedTransactions, ct);

        return Results.Ok(new BudgetSyncResponse(
            mergedBudgets.Budgets, mergedBudgets.Tombstones,
            mergedTransactions.Transactions, mergedTransactions.Tombstones,
            DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var (budgets, transactions) = await LoadSetsAsync(db, userId, ct);
        return Results.Ok(new BudgetSyncResponse(
            budgets.Budgets, budgets.Tombstones, transactions.Transactions, transactions.Tombstones,
            DateTimeOffset.UtcNow));
    }

    private static bool TryValidate(BudgetSyncRequest request, out string error)
    {
        if (request.Budgets.Count + request.BudgetTombstones.Count > MaxEntries
            || request.Transactions.Count + request.TransactionTombstones.Count > MaxEntries)
        {
            error = $"Too many entries (max {MaxEntries} per collection).";
            return false;
        }

        foreach (var name in request.Budgets.Select(b => b.Name)
                                .Concat(request.BudgetTombstones.Select(t => t.Name)))
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            {
                error = $"Each budget name must be 1–{MaxNameLength} characters.";
                return false;
            }
        }

        error = "";
        return true;
    }

    private static async Task<(BudgetSet Budgets, TransactionSet Transactions)> LoadSetsAsync(
        SyncDbContext db, string userId, CancellationToken ct)
    {
        var budgetRows = await db.Budgets
            .Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var txRows = await db.BudgetTransactions
            .Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);

        var budgets    = new List<BudgetDto>();
        var budgetTombs = new List<BudgetTombstone>();
        foreach (var row in budgetRows)
        {
            if (row.IsDeleted)
                budgetTombs.Add(new BudgetTombstone(row.Name, row.UpdatedAtUtc));
            else
            {
                var dto = JsonSerializer.Deserialize<BudgetDto>(row.PayloadJson, PaycheckJson.Options);
                if (dto is not null) budgets.Add(dto);
            }
        }

        var transactions = new List<TransactionDto>();
        var txTombs      = new List<TransactionTombstone>();
        foreach (var row in txRows)
        {
            if (row.IsDeleted)
                txTombs.Add(new TransactionTombstone(row.Id, row.UpdatedAtUtc));
            else
            {
                var dto = JsonSerializer.Deserialize<TransactionDto>(row.PayloadJson, PaycheckJson.Options);
                if (dto is not null) transactions.Add(dto);
            }
        }

        return (new BudgetSet(budgets, budgetTombs), new TransactionSet(transactions, txTombs));
    }

    private static async Task PersistAsync(
        SyncDbContext db, string userId,
        BudgetSet mergedBudgets, TransactionSet mergedTransactions,
        CancellationToken ct)
    {
        // ── Budgets ──────────────────────────────────────────────────────────
        var budgetRows = await db.Budgets.Where(r => r.UserId == userId).ToListAsync(ct);
        var budgetsByKey = budgetRows.ToDictionary(r => r.NameKey, StringComparer.Ordinal);
        var keepBudgets = new HashSet<string>(StringComparer.Ordinal);

        void UpsertBudget(string name, DateTimeOffset ts, bool deleted, string payload)
        {
            var key = name.ToLowerInvariant();
            keepBudgets.Add(key);
            if (budgetsByKey.TryGetValue(key, out var row))
            {
                row.Name = name; row.UpdatedAtUtc = ts; row.IsDeleted = deleted; row.PayloadJson = payload;
            }
            else
            {
                db.Budgets.Add(new BudgetEntity
                    { UserId = userId, NameKey = key, Name = name, UpdatedAtUtc = ts, IsDeleted = deleted, PayloadJson = payload });
            }
        }

        foreach (var b in mergedBudgets.Budgets)
            UpsertBudget(b.Name, b.UpdatedAtUtc, false, JsonSerializer.Serialize(b, PaycheckJson.Options));
        foreach (var t in mergedBudgets.Tombstones)
            UpsertBudget(t.Name, t.DeletedAtUtc, true, string.Empty);

        foreach (var row in budgetRows)
            if (!keepBudgets.Contains(row.NameKey)) db.Budgets.Remove(row);

        // ── Transactions ─────────────────────────────────────────────────────
        var txRows = await db.BudgetTransactions.Where(r => r.UserId == userId).ToListAsync(ct);
        var txById = txRows.ToDictionary(r => r.Id);
        var keepTx = new HashSet<Guid>();

        void UpsertTx(Guid id, DateTimeOffset ts, bool deleted, string payload)
        {
            keepTx.Add(id);
            if (txById.TryGetValue(id, out var row))
            {
                row.UpdatedAtUtc = ts; row.IsDeleted = deleted; row.PayloadJson = payload;
            }
            else
            {
                db.BudgetTransactions.Add(new BudgetTransactionEntity
                    { UserId = userId, Id = id, UpdatedAtUtc = ts, IsDeleted = deleted, PayloadJson = payload });
            }
        }

        foreach (var tx in mergedTransactions.Transactions)
            UpsertTx(tx.Id, tx.UpdatedAtUtc, false, JsonSerializer.Serialize(tx, PaycheckJson.Options));
        foreach (var t in mergedTransactions.Tombstones)
            UpsertTx(t.Id, t.DeletedAtUtc, true, string.Empty);

        foreach (var row in txRows)
            if (!keepTx.Contains(row.Id)) db.BudgetTransactions.Remove(row);

        await db.SaveChangesAsync(ct);
    }
}
