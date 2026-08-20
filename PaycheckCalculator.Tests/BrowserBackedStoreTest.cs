extern alias blazor;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using blazor::PaycheckCalculator.Blazor.Services;
using Microsoft.JSInterop;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// An <see cref="IJSRuntime"/> that emulates the <c>paycheckStorage</c> helpers in
/// <c>wwwroot/export.js</c> over an in-process dictionary. Set <see cref="Reachable"/> to false to
/// emulate server-side prerendering, where JS interop throws
/// <see cref="InvalidOperationException"/>.
/// </summary>
internal sealed class FakeJsStorageRuntime(Dictionary<string, string>? backing = null) : IJSRuntime
{
    public Dictionary<string, string> Items { get; } = backing ?? new();
    public bool Reachable { get; set; } = true;
    public int Writes { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (!Reachable)
            throw new InvalidOperationException("JavaScript interop calls cannot be issued during prerendering.");

        var key = (string)args![0]!;
        switch (identifier)
        {
            case "paycheckStorage.get":
                Items.TryGetValue(key, out var value);
                return ValueTask.FromResult((TValue)(object?)value!);
            case "paycheckStorage.set":
                Items[key] = (string)args[1]!;
                Writes++;
                return ValueTask.FromResult(default(TValue)!);
            case "paycheckStorage.remove":
                Items.Remove(key);
                return ValueTask.FromResult(default(TValue)!);
            default:
                throw new InvalidOperationException($"Unexpected JS call '{identifier}'.");
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}

/// <summary>
/// Tests for the browser-localStorage persistence behind the Blazor stores: anonymous saved
/// paychecks and budgets must survive a new circuit (a refresh or a reopened tab), and must
/// degrade to the previous in-memory-only behaviour whenever the browser is out of reach.
/// </summary>
public sealed class BrowserBackedStoreTest
{
    private static SavedPaycheckDto Paycheck(string name, decimal net) => new()
    {
        Name = name,
        UpdatedAtUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
        Input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            HourlyRate = 25m,
            RegularHours = 80m
        },
        Result = new SavedPaycheckResultDto { GrossPay = 2000m, NetPay = net }
    };

    private static BudgetDto Budget(string name, decimal income) => new()
    {
        Name = name,
        MonthlyNetIncome = income,
        UpdatedAtUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
    };

    // ── Saved paychecks ──────────────────────────────────────────

    [Fact]
    public async Task Paychecks_SurviveANewCircuit()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.UpsertAsync(Paycheck("Job 1", 1500m));

        // A refresh means a brand-new store over the same browser storage.
        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await second.LoadAsync();

        var restored = Assert.Single(set.Paychecks);
        Assert.Equal("Job 1", restored.Name);
        Assert.Equal(1500m, restored.Result!.NetPay);
    }

    [Fact]
    public async Task PaycheckTombstones_SurviveANewCircuit()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.UpsertAsync(Paycheck("Job 1", 1500m));
        await first.RemoveAsync("Job 1", new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await second.LoadAsync();

        Assert.Empty(set.Paychecks);
        Assert.Single(set.Tombstones);
    }

    [Fact]
    public async Task PaycheckClear_PersistsTheDeletion()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.UpsertAsync(Paycheck("Job 1", 1500m));
        await first.UpsertAsync(Paycheck("Job 2", 1700m));
        await first.ClearAsync(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await second.LoadAsync();

        Assert.Empty(set.Paychecks);
        Assert.Equal(2, set.Tombstones.Count);
    }

    [Fact]
    public async Task PaycheckReplaceAll_OverwritesStoredData()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.UpsertAsync(Paycheck("Job 1", 1500m));
        // A post-sync merge result replaces everything wholesale.
        await first.ReplaceAllAsync(new SavedPaycheckSet([Paycheck("Job 9", 2500m)], []));

        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await second.LoadAsync();

        var restored = Assert.Single(set.Paychecks);
        Assert.Equal("Job 9", restored.Name);
    }

    [Fact]
    public async Task Paychecks_WhileBrowserUnreachable_StayInMemoryOnly()
    {
        // Server-side prerender: interop throws, so nothing is written and nothing is lost.
        var js = new FakeJsStorageRuntime { Reachable = false };
        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));

        await store.UpsertAsync(Paycheck("Job 1", 1500m));
        var set = await store.LoadAsync();

        Assert.Single(set.Paychecks);
        Assert.Empty(js.Items);
    }

    [Fact]
    public async Task Paychecks_HydrateOnceTheBrowserBecomesReachable()
    {
        var js = new FakeJsStorageRuntime();
        var seed = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await seed.UpsertAsync(Paycheck("Job 1", 1500m));

        // New circuit: the prerender pass sees nothing...
        js.Reachable = false;
        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        Assert.Empty((await store.LoadAsync()).Paychecks);

        // ...and the first interactive render picks the stored data up.
        js.Reachable = true;
        await store.EnsureHydratedAsync();

        Assert.Single((await store.LoadAsync()).Paychecks);
    }

    [Fact]
    public async Task Paychecks_WrittenThisCircuit_WinOverStoredCopies()
    {
        var js = new FakeJsStorageRuntime();
        var seed = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await seed.UpsertAsync(Paycheck("Job 1", 1500m));

        // A store that took a write before it managed to hydrate must keep the newer value.
        js.Reachable = false;
        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await store.UpsertAsync(Paycheck("Job 1", 9999m));

        js.Reachable = true;
        await store.EnsureHydratedAsync();

        var restored = Assert.Single((await store.LoadAsync()).Paychecks);
        Assert.Equal(9999m, restored.Result!.NetPay);
    }

    [Fact]
    public async Task Paychecks_CorruptPayload_IsDiscardedRatherThanThrowing()
    {
        var js = new FakeJsStorageRuntime();
        js.Items["paycheckcalc.paychecks.v1"] = "{ this is not json";

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await store.LoadAsync();

        Assert.Empty(set.Paychecks);
        Assert.False(js.Items.ContainsKey("paycheckcalc.paychecks.v1"));
    }

    [Fact]
    public async Task Paychecks_NewerSchemaVersion_IsLeftUntouched()
    {
        // A payload from a future build must not be parsed into a half-understood state.
        var js = new FakeJsStorageRuntime();
        const string future = """{"SchemaVersion":99,"Paychecks":[],"Tombstones":[]}""";
        js.Items["paycheckcalc.paychecks.v1"] = future;

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var set = await store.LoadAsync();

        Assert.Empty(set.Paychecks);
        Assert.Equal(future, js.Items["paycheckcalc.paychecks.v1"]);
    }

    // ── Budgets ──────────────────────────────────────────────────

    [Fact]
    public async Task Budgets_SurviveANewCircuit()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionBudgetStore(new BrowserLocalStorage(js));
        await first.UpsertBudgetAsync(Budget("Household", 4200m));

        var second = new SessionBudgetStore(new BrowserLocalStorage(js));
        var restored = Assert.Single((await second.LoadBudgetsAsync()).Budgets);

        Assert.Equal("Household", restored.Name);
        Assert.Equal(4200m, restored.MonthlyNetIncome);
    }

    [Fact]
    public async Task AllFourBudgetDomains_ShareOneEnvelopeAndSurviveTogether()
    {
        var js = new FakeJsStorageRuntime();
        var txId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var goalId = Guid.NewGuid();

        var first = new SessionBudgetStore(new BrowserLocalStorage(js));
        await first.UpsertBudgetAsync(Budget("Household", 4200m));
        await first.UpsertTransactionAsync(new TransactionDto
        {
            Id = txId, BudgetName = "Household", CategoryName = "Groceries", Amount = 82.15m
        });
        await first.UpsertRecurringBillAsync(new RecurringBillDto
        {
            Id = billId, BudgetName = "Household", Name = "Rent",
            CategoryName = "Housing", Amount = 1800m
        });
        await first.UpsertSavingsGoalAsync(new SavingsGoalDto
        {
            Id = goalId, BudgetName = "Household", Name = "Emergency Fund", TargetAmount = 10000m
        });

        var second = new SessionBudgetStore(new BrowserLocalStorage(js));

        Assert.Single((await second.LoadBudgetsAsync()).Budgets);
        Assert.Equal(txId, Assert.Single((await second.LoadTransactionsAsync()).Transactions).Id);
        Assert.Equal(billId, Assert.Single((await second.LoadRecurringBillsAsync()).Bills).Id);
        Assert.Equal(goalId, Assert.Single((await second.LoadSavingsGoalsAsync()).Goals).Id);
        Assert.Single(js.Items);
    }

    [Fact]
    public async Task BudgetTombstones_SurviveANewCircuit()
    {
        var js = new FakeJsStorageRuntime();

        var first = new SessionBudgetStore(new BrowserLocalStorage(js));
        await first.UpsertBudgetAsync(Budget("Household", 4200m));
        await first.RemoveBudgetAsync("Household", new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

        var second = new SessionBudgetStore(new BrowserLocalStorage(js));
        var set = await second.LoadBudgetsAsync();

        Assert.Empty(set.Budgets);
        Assert.Single(set.Tombstones);
    }

    [Fact]
    public async Task Budgets_WhileBrowserUnreachable_StayInMemoryOnly()
    {
        var js = new FakeJsStorageRuntime { Reachable = false };
        var store = new SessionBudgetStore(new BrowserLocalStorage(js));

        await store.UpsertBudgetAsync(Budget("Household", 4200m));

        Assert.Single((await store.LoadBudgetsAsync()).Budgets);
        Assert.Empty(js.Items);
    }

    [Fact]
    public async Task Budgets_CorruptPayload_IsDiscardedRatherThanThrowing()
    {
        var js = new FakeJsStorageRuntime();
        js.Items["paycheckcalc.budgets.v1"] = "not json at all";

        var store = new SessionBudgetStore(new BrowserLocalStorage(js));

        Assert.Empty((await store.LoadBudgetsAsync()).Budgets);
        Assert.False(js.Items.ContainsKey("paycheckcalc.budgets.v1"));
    }

    [Fact]
    public async Task Budgets_NewerSchemaVersion_IsLeftUntouched()
    {
        var js = new FakeJsStorageRuntime();
        const string future = """{"SchemaVersion":99,"Budgets":[]}""";
        js.Items["paycheckcalc.budgets.v1"] = future;

        var store = new SessionBudgetStore(new BrowserLocalStorage(js));

        Assert.Empty((await store.LoadBudgetsAsync()).Budgets);
        Assert.Equal(future, js.Items["paycheckcalc.budgets.v1"]);
    }
}
