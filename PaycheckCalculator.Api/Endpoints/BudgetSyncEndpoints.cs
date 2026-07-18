using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalculator.Api.Data;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Json;

namespace PaycheckCalculator.Api.Endpoints;

/// <summary>
/// Authorized budget sync endpoints. <c>POST /sync</c> merges the client's pushed state (budgets,
/// transactions, recurring bills, and savings goals) with the stored state (server-side, via
/// <see cref="BudgetMerger"/>) and returns the merged result; <c>GET /</c> returns the stored state
/// without writing.
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
        var user = await ActiveUserResolver.GetActiveUserAsync(principal, users);
        if (user is null) return Results.Unauthorized();

        if (!TryValidate(request, out var error))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [error] });

        var existing = await LoadSetsAsync(db, user.Id, ct);
        var mergedBudgets      = BudgetMerger.MergeBudgets(existing.Budgets, new BudgetSet(request.Budgets, request.BudgetTombstones));
        var mergedTransactions = BudgetMerger.MergeTransactions(existing.Transactions, new TransactionSet(request.Transactions, request.TransactionTombstones));
        var mergedBills        = BudgetMerger.MergeRecurringBills(existing.Bills, new RecurringBillSet(request.RecurringBills, request.RecurringBillTombstones));
        var mergedGoals        = BudgetMerger.MergeSavingsGoals(existing.Goals, new SavingsGoalSet(request.SavingsGoals, request.SavingsGoalTombstones));

        await PersistAsync(db, user.Id, mergedBudgets, mergedTransactions, mergedBills, mergedGoals, ct);

        return Results.Ok(BuildResponse(mergedBudgets, mergedTransactions, mergedBills, mergedGoals));
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var user = await ActiveUserResolver.GetActiveUserAsync(principal, users);
        if (user is null) return Results.Unauthorized();

        var sets = await LoadSetsAsync(db, user.Id, ct);
        return Results.Ok(BuildResponse(sets.Budgets, sets.Transactions, sets.Bills, sets.Goals));
    }

    private static BudgetSyncResponse BuildResponse(
        BudgetSet budgets, TransactionSet transactions, RecurringBillSet bills, SavingsGoalSet goals) =>
        new(budgets.Budgets, budgets.Tombstones,
            transactions.Transactions, transactions.Tombstones,
            bills.Bills, bills.Tombstones,
            goals.Goals, goals.Tombstones,
            DateTimeOffset.UtcNow);

    private static bool TryValidate(BudgetSyncRequest request, out string error)
    {
        if (request.Budgets.Count + request.BudgetTombstones.Count > MaxEntries
            || request.Transactions.Count + request.TransactionTombstones.Count > MaxEntries
            || request.RecurringBills.Count + request.RecurringBillTombstones.Count > MaxEntries
            || request.SavingsGoals.Count + request.SavingsGoalTombstones.Count > MaxEntries)
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

    private static async Task<(BudgetSet Budgets, TransactionSet Transactions, RecurringBillSet Bills, SavingsGoalSet Goals)> LoadSetsAsync(
        SyncDbContext db, string userId, CancellationToken ct)
    {
        var budgetRows = await db.Budgets.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var txRows     = await db.BudgetTransactions.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var billRows   = await db.RecurringBills.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var goalRows   = await db.SavingsGoals.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);

        var budgets     = new List<BudgetDto>();
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

        var bills      = new List<RecurringBillDto>();
        var billTombs  = new List<RecurringBillTombstone>();
        foreach (var row in billRows)
        {
            if (row.IsDeleted)
                billTombs.Add(new RecurringBillTombstone(row.Id, row.UpdatedAtUtc));
            else
            {
                var dto = JsonSerializer.Deserialize<RecurringBillDto>(row.PayloadJson, PaycheckJson.Options);
                if (dto is not null) bills.Add(dto);
            }
        }

        var goals     = new List<SavingsGoalDto>();
        var goalTombs = new List<SavingsGoalTombstone>();
        foreach (var row in goalRows)
        {
            if (row.IsDeleted)
                goalTombs.Add(new SavingsGoalTombstone(row.Id, row.UpdatedAtUtc));
            else
            {
                var dto = JsonSerializer.Deserialize<SavingsGoalDto>(row.PayloadJson, PaycheckJson.Options);
                if (dto is not null) goals.Add(dto);
            }
        }

        return (new BudgetSet(budgets, budgetTombs),
                new TransactionSet(transactions, txTombs),
                new RecurringBillSet(bills, billTombs),
                new SavingsGoalSet(goals, goalTombs));
    }

    private static async Task PersistAsync(
        SyncDbContext db, string userId,
        BudgetSet mergedBudgets, TransactionSet mergedTransactions,
        RecurringBillSet mergedBills, SavingsGoalSet mergedGoals,
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

        // ── Recurring bills ────────────────────────────────────────────────────
        var billRows = await db.RecurringBills.Where(r => r.UserId == userId).ToListAsync(ct);
        var billById = billRows.ToDictionary(r => r.Id);
        var keepBills = new HashSet<Guid>();

        void UpsertBill(Guid id, DateTimeOffset ts, bool deleted, string payload)
        {
            keepBills.Add(id);
            if (billById.TryGetValue(id, out var row))
            {
                row.UpdatedAtUtc = ts; row.IsDeleted = deleted; row.PayloadJson = payload;
            }
            else
            {
                db.RecurringBills.Add(new RecurringBillEntity
                    { UserId = userId, Id = id, UpdatedAtUtc = ts, IsDeleted = deleted, PayloadJson = payload });
            }
        }

        foreach (var bill in mergedBills.Bills)
            UpsertBill(bill.Id, bill.UpdatedAtUtc, false, JsonSerializer.Serialize(bill, PaycheckJson.Options));
        foreach (var t in mergedBills.Tombstones)
            UpsertBill(t.Id, t.DeletedAtUtc, true, string.Empty);

        foreach (var row in billRows)
            if (!keepBills.Contains(row.Id)) db.RecurringBills.Remove(row);

        // ── Savings goals ──────────────────────────────────────────────────────
        var goalRows = await db.SavingsGoals.Where(r => r.UserId == userId).ToListAsync(ct);
        var goalById = goalRows.ToDictionary(r => r.Id);
        var keepGoals = new HashSet<Guid>();

        void UpsertGoal(Guid id, DateTimeOffset ts, bool deleted, string payload)
        {
            keepGoals.Add(id);
            if (goalById.TryGetValue(id, out var row))
            {
                row.UpdatedAtUtc = ts; row.IsDeleted = deleted; row.PayloadJson = payload;
            }
            else
            {
                db.SavingsGoals.Add(new SavingsGoalEntity
                    { UserId = userId, Id = id, UpdatedAtUtc = ts, IsDeleted = deleted, PayloadJson = payload });
            }
        }

        foreach (var goal in mergedGoals.Goals)
            UpsertGoal(goal.Id, goal.UpdatedAtUtc, false, JsonSerializer.Serialize(goal, PaycheckJson.Options));
        foreach (var t in mergedGoals.Tombstones)
            UpsertGoal(t.Id, t.DeletedAtUtc, true, string.Empty);

        foreach (var row in goalRows)
            if (!keepGoals.Contains(row.Id)) db.SavingsGoals.Remove(row);

        await db.SaveChangesAsync(ct);
    }
}
