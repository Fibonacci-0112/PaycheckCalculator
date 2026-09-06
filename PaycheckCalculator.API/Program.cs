using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.API.Endpoints;
using PaycheckCalculator.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

// Falls back to the credentials seeded by compose.yml so a plain `dotnet run` works against
// `docker compose up -d postgres` with no configuration. Override with the standard .NET form,
// e.g. ConnectionStrings__Sync='Host=...;Database=...;Username=...;Password=...'.
const string DefaultSyncConnectionString =
    "Host=localhost;Port=5432;Database=paycheckcalculator_dev;Username=admin;Password=password";

var syncConnectionString = builder.Configuration.GetConnectionString("Sync") ?? DefaultSyncConnectionString;

builder.Services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(syncConnectionString));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<SyncDbContext>();

// The sync payloads carry StateInputValues and enums, so the minimal-API JSON pipeline must use the
// same converters as the clients — otherwise the server would round-trip the data incorrectly.
builder.Services.ConfigureHttpJsonOptions(options => PaycheckJson.AddConverters(options.SerializerOptions));

var app = builder.Build();

// Production (PostgreSQL) applies EF Core migrations so the schema can evolve as new tables/columns
// are added. The integration tests swap in SQLite, which the Npgsql-targeted migrations don't apply
// to, so that path builds the schema directly from the model via EnsureCreated instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    try
    {
        if (db.Database.IsNpgsql())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }
    catch (Exception ex) when (db.Database.IsNpgsql())
    {
        // Startup still fails fast, but an unreachable or misconfigured database is by far the most
        // common way to trip over this — so say which server was tried and how to fix it instead of
        // dumping a bare Npgsql stack trace. The password is deliberately never echoed.
        var target = new NpgsqlConnectionStringBuilder(syncConnectionString);
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogCritical(
            ex,
            "Could not prepare the sync database on {Host}:{Port} (database '{Database}', user '{Username}'). " +
            "Start one with `docker compose up -d postgres`, or point the API at your own server by setting " +
            "ConnectionStrings__Sync (or ConnectionStrings:Sync in appsettings.Development.json).",
            target.Host, target.Port, target.Database, target.Username);
        return 1;
    }
}

// Email + password register/login/refresh, etc.
app.MapGroup("/api/account").MapIdentityApi<IdentityUser>();
app.MapGroup("/api/account").RequireAuthorization().MapAccountDataEndpoints();

// Authorized paycheck sync.
app.MapGroup("/api/paychecks").RequireAuthorization().MapPaycheckSyncEndpoints();

// Authorized budget + transaction sync.
app.MapGroup("/api/budgets").RequireAuthorization().MapBudgetSyncEndpoints();

app.Run();

// The startup database check above returns a non-zero exit code, so the success path must return too.
return 0;

// Exposed so the integration tests can spin up the app with WebApplicationFactory<Program>.
public partial class Program;
