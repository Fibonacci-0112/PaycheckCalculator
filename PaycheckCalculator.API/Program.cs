using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.API.Endpoints;
using PaycheckCalculator.API.Health;
using PaycheckCalculator.API.RateLimiting;
using PaycheckCalculator.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

// The connection string is required — there is no built-in fallback. A hardcoded default would let a
// misconfigured deployment start "successfully" against a local database with well-known credentials
// instead of failing loudly, so a missing value throws when the DbContext options are first built,
// which happens during the startup scope below (i.e. before the server accepts any request).
var syncConnectionString = builder.Configuration.GetConnectionString("Sync");
builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseNpgsql(!string.IsNullOrWhiteSpace(syncConnectionString)
        ? syncConnectionString
        : throw new InvalidOperationException(
            "Connection string 'Sync' is not configured. Set ConnectionStrings:Sync (for example via "
            + "the ConnectionStrings__Sync environment variable) before starting PaycheckCalculator.API.")));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<SyncDbContext>();

// Register/login/refresh are password-guessing targets and sync accepts whole snapshots, so both
// groups are throttled. See RateLimitPolicies for the partitioning and why the defaults are loose.
builder.Services.AddPaycheckRateLimiting(builder.Configuration);

// Liveness answers "is the process up"; readiness additionally requires the sync database, so an
// orchestrator can keep an instance out of rotation while its database is unreachable.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sync-database", tags: ["ready"]);

// The sync payloads carry StateInputValues and enums, so the minimal-API JSON pipeline must use the
// same converters as the clients — otherwise the server would round-trip the data incorrectly.
builder.Services.ConfigureHttpJsonOptions(options => PaycheckJson.AddConverters(options.SerializerOptions));

var app = builder.Build();

// Production (PostgreSQL) applies EF Core migrations so the schema can evolve as new tables/columns
// are added. Deployments that run migrations as a separate release step can opt out with
// Database:MigrateOnStartup=false. The integration tests swap in SQLite, which the Npgsql-targeted
// migrations don't apply to, so that path builds the schema directly from the model via EnsureCreated.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    if (!db.Database.IsNpgsql())
        db.Database.EnsureCreated();
    else if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        db.Database.Migrate();
}

app.UseRateLimiter();

// Email + password register/login/refresh, etc.
app.MapGroup("/api/account").RequireRateLimiting(RateLimitPolicies.Account).MapIdentityApi<IdentityUser>();
app.MapGroup("/api/account").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Sync).MapAccountDataEndpoints();

// Authorized paycheck sync.
app.MapGroup("/api/paychecks").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Sync).MapPaycheckSyncEndpoints();

// Authorized budget + transaction sync.
app.MapGroup("/api/budgets").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Sync).MapBudgetSyncEndpoints();

// Probes stay unauthenticated and unthrottled so they keep answering while the API is shedding load.
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Exposed so the integration tests can spin up the app with WebApplicationFactory<Program>.
public partial class Program;
