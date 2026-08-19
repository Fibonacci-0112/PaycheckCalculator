using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalculator.API.Data;
using PaycheckCalculator.API.Endpoints;
using PaycheckCalculator.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Sync") ?? "Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres"));

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
    if (db.Database.IsNpgsql())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// Email + password register/login/refresh, etc.
app.MapGroup("/api/account").MapIdentityApi<IdentityUser>();
app.MapGroup("/api/account").RequireAuthorization().MapAccountDataEndpoints();

// Authorized paycheck sync.
app.MapGroup("/api/paychecks").RequireAuthorization().MapPaycheckSyncEndpoints();

// Authorized budget + transaction sync.
app.MapGroup("/api/budgets").RequireAuthorization().MapBudgetSyncEndpoints();

app.Run();

// Exposed so the integration tests can spin up the app with WebApplicationFactory<Program>.
public partial class Program;
