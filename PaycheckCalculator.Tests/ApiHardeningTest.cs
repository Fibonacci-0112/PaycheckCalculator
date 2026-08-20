using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Sync;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Covers the operational hardening around the sync API rather than its sync semantics: the
/// liveness/readiness probes, the fixed-window rate limits on the account and sync surfaces, and the
/// startup requirement that the PostgreSQL connection string be supplied by configuration instead of
/// falling back to well-known local credentials.
/// </summary>
public sealed class ApiHardeningTest
{
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task LivenessProbe_IsAnonymousAndHealthy()
    {
        using var factory = new HardeningFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Healthy", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessProbe_ReportsHealthyWhenDatabaseReachable()
    {
        using var factory = new HardeningFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Healthy", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AccountEndpoints_RejectWithTooManyRequests_OnceWindowIsExhausted()
    {
        // Two registrations are permitted per window; the third must be throttled rather than
        // reaching Identity, which is what stops password guessing and account enumeration.
        using var factory = new HardeningFactory(accountPermitPerWindow: 2);
        var client = factory.CreateClient();

        var first = await client.PostAsync("/api/account/register", Json(new { email = NewEmail(), password = Password }));
        var second = await client.PostAsync("/api/account/register", Json(new { email = NewEmail(), password = Password }));
        var third = await client.PostAsync("/api/account/register", Json(new { email = NewEmail(), password = Password }));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.NotNull(third.Headers.RetryAfter);
    }

    [Fact]
    public async Task SyncEndpoints_RejectWithTooManyRequests_OnceWindowIsExhausted()
    {
        using var factory = new HardeningFactory(syncPermitPerWindow: 2);
        var client = await SignedInClientAsync(factory);

        var first = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        var second = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        var third = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task SyncRateLimit_IsPartitionedPerUser_SoOneAccountCannotStarveAnother()
    {
        using var factory = new HardeningFactory(syncPermitPerWindow: 2);
        var noisy = await SignedInClientAsync(factory);
        var quiet = await SignedInClientAsync(factory);

        await noisy.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        await noisy.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));
        var noisyThrottled = await noisy.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));

        // The second account shares the (absent) test-host IP, so this only succeeds if the limiter
        // partitions authenticated traffic by user id.
        var quietFirst = await quiet.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));

        Assert.Equal(HttpStatusCode.TooManyRequests, noisyThrottled.StatusCode);
        quietFirst.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UnauthenticatedSync_IsStillRejected_WhenRateLimitsAreConfigured()
    {
        using var factory = new HardeningFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsync("/api/paychecks/sync", Json(new SyncRequest([], [])));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public void Startup_FailsFast_WhenSyncConnectionStringIsMissing()
    {
        // No DbContext override here, so the production PostgreSQL registration stays in place and
        // must refuse to start rather than silently defaulting to a local database.
        using var factory = new MissingConnectionStringFactory();

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Sync", ex.Message, StringComparison.Ordinal);
    }

    private static async Task<HttpClient> SignedInClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        (await client.PostAsync("/api/account/register", Json(new { email, password = Password })))
            .EnsureSuccessStatusCode();
        var login = await client.PostAsync("/api/account/login", Json(new { email, password = Password }));
        login.EnsureSuccessStatusCode();

        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(PaycheckJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static HttpContent Json<T>(T value) => JsonContent.Create(value, options: PaycheckJson.Options);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);

    /// <summary>
    /// The sync API over in-memory SQLite, with the rate-limit windows supplied per test so the
    /// throttling assertions are deterministic instead of depending on the production defaults.
    /// </summary>
    private sealed class HardeningFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString = $"Data Source=file:apihardening-{Guid.NewGuid():N}?mode=memory&cache=shared";
        private readonly int _accountPermitPerWindow;
        private readonly int _syncPermitPerWindow;
        private SqliteConnection? _keepAlive;

        public HardeningFactory(int accountPermitPerWindow = 100_000, int syncPermitPerWindow = 100_000)
        {
            _accountPermitPerWindow = accountPermitPerWindow;
            _syncPermitPerWindow = syncPermitPerWindow;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();

            builder.UseSetting("RateLimiting:AccountPermitPerWindow", _accountPermitPerWindow.ToString());
            builder.UseSetting("RateLimiting:SyncPermitPerWindow", _syncPermitPerWindow.ToString());

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

    /// <summary>The app with its production database registration intact and no connection string.</summary>
    private sealed class MissingConnectionStringFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:Sync", string.Empty);
    }
}
