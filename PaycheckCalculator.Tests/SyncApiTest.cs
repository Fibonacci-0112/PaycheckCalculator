using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.Core.Budgeting;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Shared.Budgeting;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using PaycheckCalculator.Shared.Sync;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// End-to-end tests of the sync API over a real HTTP pipeline (<see cref="WebApplicationFactory{T}"/>)
/// backed by a shared in-memory SQLite database. Covers Identity register/login, authorization, the
/// server-side last-write-wins merge, tombstone propagation, and — most importantly — that a snapshot's
/// <see cref="StateInputValues"/> survives a full server round-trip intact.
/// </summary>
public sealed class SyncApiTest : IClassFixture<SyncApiTest.ApiFactory>
{
    private readonly ApiFactory _factory;

    public SyncApiTest(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Sync_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLogin_IssuesBearerToken()
    {
        var client = _factory.CreateClient();
        var email = NewEmail();

        var register = await client.PostAsync("/api/account/register", Json(new { email, password = Password }));
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsync("/api/account/login", Json(new { email, password = Password }));
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(PaycheckJson.Options);

        Assert.False(string.IsNullOrEmpty(token!.AccessToken));
    }

    [Fact]
    public async Task FirstSync_EchoesPushedPaychecks()
    {
        var client = await SignedInClientAsync();
        var response = await SyncAsync(client, new SyncRequest([Entry("Job A", At(1))], []));

        Assert.Single(response.Paychecks);
        Assert.Equal("Job A", response.Paychecks[0].Name);
        Assert.Empty(response.Tombstones);
    }

    [Fact]
    public async Task SecondClient_WithNewerEntry_WinsAcrossSync()
    {
        var email = NewEmail();
        var clientA = await SignedInClientAsync(email);
        var clientB = await SignedInClientAsync(email); // same account, different token

        await SyncAsync(clientA, new SyncRequest([Entry("Job", At(1), netPay: 100m)], []));
        var merged = await SyncAsync(clientB, new SyncRequest([Entry("Job", At(2), netPay: 200m)], []));

        Assert.Single(merged.Paychecks);
        Assert.Equal(200m, merged.Paychecks[0].Result.NetPay);
    }

    [Fact]
    public async Task Tombstone_PropagatesDelete()
    {
        var client = await SignedInClientAsync();

        await SyncAsync(client, new SyncRequest([Entry("Job", At(1))], []));
        var afterDelete = await SyncAsync(client, new SyncRequest([], [new SavedPaycheckTombstone("Job", At(2))]));

        Assert.Empty(afterDelete.Paychecks);
        Assert.Single(afterDelete.Tombstones);
    }

    [Fact]
    public async Task NewerEntry_BeatsOlderTombstone_AcrossSync()
    {
        var email = NewEmail();
        var client = await SignedInClientAsync(email);

        await SyncAsync(client, new SyncRequest([], [new SavedPaycheckTombstone("Job", At(1))]));
        var merged = await SyncAsync(client, new SyncRequest([Entry("Job", At(2))], []));

        Assert.Single(merged.Paychecks);
        Assert.Empty(merged.Tombstones);
    }

    [Fact]
    public async Task StateInputValues_SurviveServerRoundTrip()
    {
        var client = await SignedInClientAsync();
        var entry = new SavedPaycheckDto
        {
            Name = "California Job",
            UpdatedAtUtc = At(1),
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

        await SyncAsync(client, new SyncRequest([entry], []));
        var pulled = await GetAsync(client);

        var values = pulled.Paychecks.Single().Input.StateInputValues!;
        Assert.Equal("Single", values.GetValueOrDefault("FilingStatus", ""));
        Assert.Equal(2, values.GetValueOrDefault("Allowances", 0));
        Assert.Equal(15.25m, values.GetValueOrDefault("AdditionalWithholding", 0m));
        Assert.False(values.GetValueOrDefault("Exempt", true));
    }

    [Fact]
    public async Task ExportAccountData_ReturnsPaychecksAndBudgets()
    {
        var client = await SignedInClientAsync();
        await SyncAsync(client, new SyncRequest([Entry("Export Job", At(1), netPay: 432.10m)], []));
        await SyncBudgetsAsync(client, new BudgetSyncRequest(
            [
                new BudgetDto
                {
                    Name = "Household",
                    UpdatedAtUtc = At(1),
                    MonthlyNetIncome = 5000m,
                    Method = BudgetMethod.Custom,
                    Categories = [new BudgetCategoryDto { Name = "Needs", Amount = 2500m }]
                }
            ],
            [],
            [],
            [],
            [],
            [],
            [],
            []));

        var exportJson = await ExportRawAsync(client);
        using var doc = JsonDocument.Parse(exportJson);

        Assert.Equal("Export Job", doc.RootElement.GetProperty("paychecks").GetProperty("paychecks")[0].GetProperty("name").GetString());
        Assert.Equal("Household", doc.RootElement.GetProperty("budgets").GetProperty("budgets")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteAccount_RemovesAccountAndSyncedRows()
    {
        var email = NewEmail();
        var client = await SignedInClientAsync(email);
        await SyncAsync(client, new SyncRequest([Entry("Delete Me", At(1))], []));
        await SyncBudgetsAsync(client, new BudgetSyncRequest(
            [
                new BudgetDto
                {
                    Name = "Delete Budget",
                    UpdatedAtUtc = At(1),
                    MonthlyNetIncome = 1000m,
                    Method = BudgetMethod.Custom,
                    Categories = [new BudgetCategoryDto { Name = "Needs", Amount = 500m }]
                }
            ],
            [],
            [],
            [],
            [],
            [],
            [],
            []));

        var deleteResp = await client.DeleteAsync("/api/account/");
        deleteResp.EnsureSuccessStatusCode();

        var syncAfterDelete = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        Assert.Equal(HttpStatusCode.Unauthorized, syncAfterDelete.StatusCode);

        var budgetSyncAfterDelete = await client.PostAsync("/api/budgets/sync", Json(new BudgetSyncRequest([], [], [], [], [], [], [], [])));
        Assert.Equal(HttpStatusCode.Unauthorized, budgetSyncAfterDelete.StatusCode);

        var login = await _factory.CreateClient().PostAsync("/api/account/login", Json(new { email, password = Password }));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private const string Password = "Passw0rd!";
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int monthsFromBase) => Base.AddMonths(monthsFromBase);
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static SavedPaycheckDto Entry(string name, DateTimeOffset updatedAt, decimal netPay = 100m) => new()
    {
        Name = name,
        UpdatedAtUtc = updatedAt,
        Input = new PaycheckInput { State = UsState.TX },
        Result = new SavedPaycheckResultDto { NetPay = netPay }
    };

    private Task<HttpClient> SignedInClientAsync() => SignedInClientAsync(NewEmail());

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var client = _factory.CreateClient();
        // Register may 400 if the email already exists (a second token for the same account); ignore that.
        await client.PostAsync("/api/account/register", Json(new { email, password = Password }));

        var login = await client.PostAsync("/api/account/login", Json(new { email, password = Password }));
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(PaycheckJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static async Task<SyncResponse> SyncAsync(HttpClient client, SyncRequest request)
    {
        var resp = await client.PostAsync("/api/paychecks/sync", Json(request));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SyncResponse>(PaycheckJson.Options))!;
    }

    private static async Task<SyncResponse> GetAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/paychecks/");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SyncResponse>(PaycheckJson.Options))!;
    }

    private static async Task<BudgetSyncResponse> SyncBudgetsAsync(HttpClient client, BudgetSyncRequest request)
    {
        var resp = await client.PostAsync("/api/budgets/sync", Json(request));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BudgetSyncResponse>(PaycheckJson.Options))!;
    }

    private static async Task<string> ExportRawAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/account/export");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    private static HttpContent Json<T>(T value) => JsonContent.Create(value, options: PaycheckJson.Options);

    private sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);

    /// <summary>
    /// Runs the real API against a shared in-memory SQLite database, overriding the production
    /// PostgreSQL provider so the suite requires no database server. One connection is held open for
    /// the factory's lifetime so the schema (created by the app's startup <c>EnsureCreated</c>)
    /// persists across request-scoped contexts.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString = $"Data Source=file:paychecksync-{Guid.NewGuid():N}?mode=memory&cache=shared";
        private SqliteConnection? _keepAlive;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();

            // The test host has no remote IP, so every request lands in one rate-limit partition.
            // Pin the windows high so this suite exercises sync behavior, not throttling; the 429
            // paths get their own deliberately tiny limits in ApiHardeningTest.
            builder.UseSetting("RateLimiting:AccountPermitPerWindow", "100000");
            builder.UseSetting("RateLimiting:SyncPermitPerWindow", "100000");

            // Production wires PostgreSQL; drop every EF registration bound to SyncDbContext (the
            // options and any provider configuration) so only the in-memory SQLite override remains.
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                    .Where(d => d.ServiceType == typeof(DbContextOptions)
                        || (d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericArguments().Contains(typeof(SyncDbContext))))
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<SyncDbContext>(options => options.UseSqlite(_connectionString));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _keepAlive?.Dispose();
        }
    }
}
