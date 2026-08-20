extern alias blazor;
using blazor::PaycheckCalculator.Blazor.Services;
using System.Text.Json;
using Microsoft.JSInterop;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Snapshots;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for the Blazor session stores' browser-<c>localStorage</c> persistence. Anonymous web data
/// used to vanish when the tab closed; it is now mirrored through <see cref="BrowserLocalStorage"/>
/// so it survives a reload. These tests stand in for the browser with a fake
/// <see cref="IJSRuntime"/> that emulates <c>window.localStorage</c>.
/// </summary>
public sealed class BrowserPersistenceTest
{
    /// <summary>
    /// Emulates <c>wwwroot/localStore.js</c> over a dictionary. <see cref="Unavailable"/> models
    /// prerendering / blocked storage, where every interop call throws.
    /// </summary>
    private sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Storage { get; } = [];
        public bool Unavailable { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (Unavailable)
                throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");

            var key = (string)args![0]!;
            switch (identifier)
            {
                case "paycheckLocalStore.get":
                    return ValueTask.FromResult(
                        Storage.TryGetValue(key, out var value) ? (TValue)(object)value : default!);
                case "paycheckLocalStore.set":
                    Storage[key] = (string)args[1]!;
                    return default;
                case "paycheckLocalStore.remove":
                    Storage.Remove(key);
                    return default;
                default:
                    throw new InvalidOperationException($"Unexpected interop call: {identifier}");
            }
        }
    }

    private static SavedPaycheckDto Paycheck(string name) => new()
    {
        Name = name,
        UpdatedAtUtc = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
        Input = new PaycheckInput
        {
            State = UsState.CA,
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 2,
                ["AdditionalWithholding"] = 15.25m,
                ["Exempt"] = false
            }
        },
        Result = new SavedPaycheckResultDto { NetPay = 1234.56m }
    };

    // ── Saved paychecks ───────────────────────────────────────────

    [Fact]
    public async Task SavedPaychecks_SurviveANewCircuit()
    {
        var js = new FakeJsRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.HydrateAsync();
        await first.UpsertAsync(Paycheck("Main job"));

        // A new circuit (browser reload) gets a brand-new store over the same browser storage.
        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        var restored = await second.HydrateAsync();

        Assert.True(restored);
        var set = await second.LoadAsync();
        Assert.Equal("Main job", Assert.Single(set.Paychecks).Name);
    }

    [Fact]
    public async Task StateInputValues_RoundTripAsClrPrimitives()
    {
        // The persisted snapshot must go through PaycheckJson.Options so StateInputValuesJsonConverter
        // stays in charge: consumers must never see a JsonElement, and typed reads must keep working.
        var js = new FakeJsRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.HydrateAsync();
        await first.UpsertAsync(Paycheck("Main job"));

        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await second.HydrateAsync();
        var restored = Assert.Single((await second.LoadAsync()).Paychecks);

        var values = restored.Input.StateInputValues!;
        Assert.All(values.Values, v => Assert.IsNotType<JsonElement>(v));
        Assert.Equal("Single", values.GetValueOrDefault("FilingStatus", ""));
        Assert.Equal(2, values.GetValueOrDefault("Allowances", 0));
        Assert.Equal(15.25m, values.GetValueOrDefault("AdditionalWithholding", 0m));
        Assert.False(values.GetValueOrDefault("Exempt", true));
    }

    [Fact]
    public async Task Tombstones_SurviveANewCircuit()
    {
        var deletedAt = new DateTimeOffset(2026, 4, 2, 9, 30, 0, TimeSpan.Zero);
        var js = new FakeJsRuntime();

        var first = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await first.HydrateAsync();
        await first.UpsertAsync(Paycheck("Old job"));
        await first.RemoveAsync("Old job", deletedAt);

        var second = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await second.HydrateAsync();
        var set = await second.LoadAsync();

        Assert.Empty(set.Paychecks);
        var tombstone = Assert.Single(set.Tombstones);
        Assert.Equal("Old job", tombstone.Name);
        Assert.Equal(deletedAt, tombstone.DeletedAtUtc);
    }

    [Fact]
    public async Task ForgetPersisted_RemovesTheBrowserCopyEntirely()
    {
        var js = new FakeJsRuntime();

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await store.HydrateAsync();
        await store.UpsertAsync(Paycheck("Main job"));
        Assert.NotEmpty(js.Storage);

        await store.ForgetPersistedAsync();

        Assert.Empty(js.Storage);
    }

    [Fact]
    public async Task WritesBeforeHydration_DoNotOverwriteStoredData()
    {
        // Prerendering runs before JS interop is available: a write at that point must not
        // clobber the snapshot the user already has in their browser.
        var js = new FakeJsRuntime();

        var seed = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await seed.HydrateAsync();
        await seed.UpsertAsync(Paycheck("Main job"));

        var prerendering = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await prerendering.ReplaceAllAsync(new SavedPaycheckSet([], []));

        var afterReload = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await afterReload.HydrateAsync();

        Assert.Equal("Main job", Assert.Single((await afterReload.LoadAsync()).Paychecks).Name);
    }

    [Fact]
    public async Task UnavailableStorage_LeavesTheStoreFullyUsableInMemory()
    {
        // Private browsing / blocked storage must degrade to in-memory, never throw.
        var js = new FakeJsRuntime { Unavailable = true };

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        Assert.False(await store.HydrateAsync());

        await store.UpsertAsync(Paycheck("Main job"));

        Assert.Equal("Main job", Assert.Single((await store.LoadAsync()).Paychecks).Name);
        Assert.Empty(js.Storage);
    }

    [Fact]
    public async Task CorruptStoredJson_IsDiscardedRatherThanThrowing()
    {
        var js = new FakeJsRuntime();
        js.Storage["paycheckcalculator.paychecks"] = "{ not json";

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));

        Assert.False(await store.HydrateAsync());
        Assert.Empty((await store.LoadAsync()).Paychecks);
        Assert.Empty(js.Storage);
    }

    [Fact]
    public async Task StoredSnapshotFromAnotherSchemaVersion_IsDiscarded()
    {
        var js = new FakeJsRuntime();
        js.Storage["paycheckcalculator.paychecks"] =
            """{"SchemaVersion":99,"Paychecks":[],"Tombstones":[]}""";

        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));

        Assert.False(await store.HydrateAsync());
        Assert.Empty((await store.LoadAsync()).Paychecks);
        Assert.Empty(js.Storage);
    }

    [Fact]
    public async Task HydrateIsIdempotent_AndNeverClobbersSessionData()
    {
        var js = new FakeJsRuntime();
        var store = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await store.HydrateAsync();
        await store.UpsertAsync(Paycheck("Main job"));

        Assert.False(await store.HydrateAsync());

        Assert.Equal("Main job", Assert.Single((await store.LoadAsync()).Paychecks).Name);
    }

    // ── Budgets ───────────────────────────────────────────────────

    [Fact]
    public async Task Budgets_SurviveANewCircuit()
    {
        var js = new FakeJsRuntime();

        var first = new SessionBudgetStore(new BrowserLocalStorage(js));
        await first.HydrateAsync();
        await first.UpsertBudgetAsync(new BudgetDto
        {
            Name = "Household",
            MonthlyNetIncome = 4200m,
            UpdatedAtUtc = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)
        });

        var second = new SessionBudgetStore(new BrowserLocalStorage(js));
        var restored = await second.HydrateAsync();

        Assert.True(restored);
        var budget = Assert.Single((await second.LoadBudgetsAsync()).Budgets);
        Assert.Equal("Household", budget.Name);
        Assert.Equal(4200m, budget.MonthlyNetIncome);
    }

    [Fact]
    public async Task TransactionsBillsAndGoals_SurviveANewCircuit()
    {
        var js = new FakeJsRuntime();
        var txId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var updated = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);

        var first = new SessionBudgetStore(new BrowserLocalStorage(js));
        await first.HydrateAsync();
        await first.UpsertTransactionAsync(new TransactionDto { Id = txId, BudgetName = "Household", CategoryName = "Groceries", Amount = 42.50m, UpdatedAtUtc = updated });
        await first.UpsertRecurringBillAsync(new RecurringBillDto { Id = billId, BudgetName = "Household", Name = "Internet", CategoryName = "Utilities", Amount = 120m, UpdatedAtUtc = updated });
        await first.UpsertSavingsGoalAsync(new SavingsGoalDto { Id = goalId, BudgetName = "Household", Name = "Emergency fund", TargetAmount = 5000m, UpdatedAtUtc = updated });

        var second = new SessionBudgetStore(new BrowserLocalStorage(js));
        await second.HydrateAsync();

        Assert.Equal(txId, Assert.Single((await second.LoadTransactionsAsync()).Transactions).Id);
        Assert.Equal(billId, Assert.Single((await second.LoadRecurringBillsAsync()).Bills).Id);
        Assert.Equal(goalId, Assert.Single((await second.LoadSavingsGoalsAsync()).Goals).Id);
    }

    [Fact]
    public async Task BudgetForgetPersisted_RemovesTheBrowserCopyEntirely()
    {
        var js = new FakeJsRuntime();

        var store = new SessionBudgetStore(new BrowserLocalStorage(js));
        await store.HydrateAsync();
        await store.UpsertBudgetAsync(new BudgetDto { Name = "Household", MonthlyNetIncome = 1m });
        Assert.NotEmpty(js.Storage);

        await store.ForgetPersistedAsync();

        Assert.Empty(js.Storage);
    }

    [Fact]
    public async Task BudgetsAndPaychecks_UseSeparateStorageKeys()
    {
        var js = new FakeJsRuntime();

        var paychecks = new SessionPaycheckStore(new BrowserLocalStorage(js));
        await paychecks.HydrateAsync();
        await paychecks.UpsertAsync(Paycheck("Main job"));

        var budgets = new SessionBudgetStore(new BrowserLocalStorage(js));
        await budgets.HydrateAsync();
        await budgets.UpsertBudgetAsync(new BudgetDto { Name = "Household", MonthlyNetIncome = 1m });

        Assert.Equal(2, js.Storage.Count);
        Assert.Contains("paycheckcalculator.paychecks", js.Storage.Keys);
        Assert.Contains("paycheckcalculator.budgets", js.Storage.Keys);
    }

    [Fact]
    public async Task BudgetUnavailableStorage_LeavesTheStoreFullyUsableInMemory()
    {
        var js = new FakeJsRuntime { Unavailable = true };

        var store = new SessionBudgetStore(new BrowserLocalStorage(js));
        Assert.False(await store.HydrateAsync());

        await store.UpsertBudgetAsync(new BudgetDto { Name = "Household", MonthlyNetIncome = 1m });

        Assert.Equal("Household", Assert.Single((await store.LoadBudgetsAsync()).Budgets).Name);
        Assert.Empty(js.Storage);
    }
}
