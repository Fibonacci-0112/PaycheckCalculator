using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Api.Data;
using PaycheckCalc.Api.Endpoints;
using PaycheckCalc.Shared.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sync") ?? "Data Source=paycheckcalc-sync.db"));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<SyncDbContext>();

// The sync payloads carry StateInputValues and enums, so the minimal-API JSON pipeline must use the
// same converters as the clients — otherwise the server would round-trip the data incorrectly.
builder.Services.ConfigureHttpJsonOptions(options => PaycheckJson.AddConverters(options.SerializerOptions));

var app = builder.Build();

// Greenfield store: create the schema on startup rather than running migrations.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<SyncDbContext>().Database.EnsureCreated();
}

// Email + password register/login/refresh, etc.
app.MapGroup("/api/account").MapIdentityApi<IdentityUser>();

// Authorized paycheck sync.
app.MapGroup("/api/paychecks").RequireAuthorization().MapPaycheckSyncEndpoints();

app.Run();

// Exposed so the integration tests can spin up the app with WebApplicationFactory<Program>.
public partial class Program;
