using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.API.Endpoints;

/// <summary>
/// Authorized account-data endpoints: export the authenticated account's synced data and
/// permanently delete the account plus all synced rows.
/// </summary>
public static class AccountDataEndpoints
{
    public static RouteGroupBuilder MapAccountDataEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/export", ExportAsync);
        group.MapDelete("/", DeleteAsync);
        return group;
    }

    private static async Task<IResult> ExportAsync(
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var user = await ActiveUserResolver.GetActiveUserAsync(principal, users);
        if (user is null) return Results.Unauthorized();

        var paychecks = await LoadPaychecksAsync(db, user.Id, ct);
        var budgets = await LoadBudgetDataAsync(db, user.Id, ct);

        var now = DateTimeOffset.UtcNow;

        return Results.Ok(new
        {
            exportedAtUtc = now,
            email = user.Email,
            paychecks = new SyncResponse(paychecks.Paychecks, paychecks.Tombstones, now),
            budgets = new BudgetSyncResponse(
                budgets.Budgets.Budgets,
                budgets.Budgets.Tombstones,
                budgets.Transactions.Transactions,
                budgets.Transactions.Tombstones,
                budgets.Bills.Bills,
                budgets.Bills.Tombstones,
                budgets.Goals.Goals,
                budgets.Goals.Tombstones,
                now)
        });
    }

    private static async Task<IResult> DeleteAsync(
        ClaimsPrincipal principal,
        SyncDbContext db,
        UserManager<IdentityUser> users,
        CancellationToken ct)
    {
        var user = await ActiveUserResolver.GetActiveUserAsync(principal, users);
        if (user is null) return Results.Unauthorized();

        await db.SavedPaychecks.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.Budgets.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.BudgetTransactions.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.RecurringBills.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.SavingsGoals.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["account"] = result.Errors.Select(e => e.Description).ToArray()
            });
        }

        return Results.Ok();
    }

    private static async Task<SavedPaycheckSet> LoadPaychecksAsync(SyncDbContext db, string userId, CancellationToken ct)
    {
        var rows = await db.SavedPaychecks.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var paychecks = new List<SavedPaycheckDto>();
        var tombstones = new List<SavedPaycheckTombstone>();

        foreach (var row in rows)
        {
            if (row.IsDeleted)
            {
                tombstones.Add(new SavedPaycheckTombstone(row.Name, row.UpdatedAtUtc));
                continue;
            }

            var dto = JsonSerializer.Deserialize<SavedPaycheckDto>(row.PayloadJson, PaycheckJson.Options);
            if (dto is not null) paychecks.Add(dto);
        }

        return new SavedPaycheckSet(paychecks, tombstones);
    }

    private static async Task<(BudgetSet Budgets, TransactionSet Transactions, RecurringBillSet Bills, SavingsGoalSet Goals)> LoadBudgetDataAsync(
        SyncDbContext db, string userId, CancellationToken ct)
    {
        var budgetRows = await db.Budgets.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var txRows = await db.BudgetTransactions.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var billRows = await db.RecurringBills.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);
        var goalRows = await db.SavingsGoals.Where(r => r.UserId == userId).AsNoTracking().ToListAsync(ct);

        var budgets = new List<BudgetDto>();
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
        var txTombs = new List<TransactionTombstone>();
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

        var bills = new List<RecurringBillDto>();
        var billTombs = new List<RecurringBillTombstone>();
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

        var goals = new List<SavingsGoalDto>();
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

        return (
            new BudgetSet(budgets, budgetTombs),
            new TransactionSet(transactions, txTombs),
            new RecurringBillSet(bills, billTombs),
            new SavingsGoalSet(goals, goalTombs));
    }
}
