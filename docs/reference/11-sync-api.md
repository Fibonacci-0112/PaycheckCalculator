# 11 — The Sync API

`PaycheckCalculator.API` is a standalone ASP.NET Core minimal-API project: ASP.NET Core Identity
email/password accounts plus authorized sync endpoints for paychecks and budgets, backed by EF
Core over PostgreSQL (SQLite in integration tests). It references `Shared` and **must never
reference `App` or `Blazor`** — the front-ends talk to it exclusively over HTTP through
`PaycheckApiClient`.

---

## `Program.cs` — composition

```csharp
var builder = WebApplication.CreateBuilder(args);

const string DefaultSyncConnectionString =
    "Host=localhost;Port=5432;Database=paycheckcalculator_dev;Username=admin;Password=password";

var syncConnectionString = builder.Configuration.GetConnectionString("Sync") ?? DefaultSyncConnectionString;

builder.Services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(syncConnectionString));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<SyncDbContext>();

// The sync payloads carry StateInputValues and enums, so the minimal-API JSON pipeline must use
// the same converters as the clients — otherwise the server would round-trip the data incorrectly.
builder.Services.ConfigureHttpJsonOptions(options => PaycheckJson.AddConverters(options.SerializerOptions));

var app = builder.Build();

// Production (PostgreSQL) applies EF Core migrations; the SQLite-backed integration tests build
// the schema directly from the model via EnsureCreated instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    if (db.Database.IsNpgsql()) db.Database.Migrate();
    else                        db.Database.EnsureCreated();
}

app.MapGroup("/api/account").MapIdentityApi<IdentityUser>();
app.MapGroup("/api/account").RequireAuthorization().MapAccountDataEndpoints();
app.MapGroup("/api/paychecks").RequireAuthorization().MapPaycheckSyncEndpoints();
app.MapGroup("/api/budgets").RequireAuthorization().MapBudgetSyncEndpoints();

app.Run();

// Exposed so integration tests can spin up the app with WebApplicationFactory<Program>.
public partial class Program;
```

Three things worth flagging:

- **`ConfigureHttpJsonOptions` reuses `PaycheckJson.AddConverters`** — the exact same converter
  set the clients use. Skipping this would make the server accept/emit `StateInputValues` and
  enums in a shape the clients can't correctly round-trip. See
  [10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md#json-configuration-sharedjson).
- **The migrate-vs-`EnsureCreated` branch** is provider-driven, not environment-driven — it
  checks `db.Database.IsNpgsql()` at runtime. Migrations target Npgsql specifically and don't
  apply against SQLite, so the test path builds the schema straight from the current model
  instead.
- **`public partial class Program;`** at the bottom is the hook `WebApplicationFactory<Program>`
  needs to host the whole app in-process for `SyncApiTest`.

---

## Identity endpoints

`AddIdentityApiEndpoints<IdentityUser>()` + `MapIdentityApi<IdentityUser>()` wire the standard
ASP.NET Core Identity bearer-token API under `/api/account`:

| Route | Purpose |
|---|---|
| `POST /api/account/register` | Create an account (email + password) |
| `POST /api/account/login` | Returns `{ tokenType, accessToken, expiresIn, refreshToken }` |
| `POST /api/account/refresh` | Exchanges a refresh token for a new access token |
| *(and the other standard Identity API routes — confirm email, forgot/reset password, 2FA, etc.)* | |

There is **no server-side logout** with the bearer-token scheme — `PaycheckApiClient.LogoutAsync`
simply clears the locally stored tokens; the previously issued access token remains technically
valid until it naturally expires.

### `AccountDataEndpoints` — export and delete

Registered under `/api/account`, `RequireAuthorization()`'d as a group:

```csharp
group.MapGet("/export", ExportAsync);      // GET  /api/account/export
group.MapDelete("/", DeleteAsync);         // DELETE /api/account/
```

**Export** resolves the current user, loads every synced collection (paychecks, budgets,
transactions, recurring bills, savings goals) via the same row-loading logic the sync endpoints
use, and returns one JSON object bundling all of it plus `exportedAtUtc` and the account email —
a complete self-service data export.

**Delete** removes every row across all five tables for the user
(`ExecuteDeleteAsync`, a bulk SQL delete with no per-row tracking overhead), then deletes the
Identity user itself:

```csharp
await db.SavedPaychecks.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
await db.Budgets.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
await db.BudgetTransactions.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
await db.RecurringBills.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);
await db.SavingsGoals.Where(r => r.UserId == user.Id).ExecuteDeleteAsync(ct);

var result = await users.DeleteAsync(user);
```

Row deletion happens **before** the Identity user record itself is removed — the data is gone
first, the account second.

### `ActiveUserResolver`

A three-line internal helper every authorized endpoint calls first:

```csharp
internal static class ActiveUserResolver
{
    public static async Task<IdentityUser?> GetActiveUserAsync(ClaimsPrincipal principal, UserManager<IdentityUser> users)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return null;
        return await users.FindByIdAsync(userId);
    }
}
```

Every sync/export/delete handler starts with `var user = await ActiveUserResolver.GetActiveUserAsync(...); if (user is null) return Results.Unauthorized();`
— a consistent, centralized "who is this request actually for" check even though the endpoint is
already behind `RequireAuthorization()`.

---

## `PaycheckSyncEndpoints`

Mounted at `/api/paychecks`, authorized as a group:

```csharp
group.MapPost("/sync", SyncAsync);   // push + merge, returns merged state
group.MapGet("/", GetAsync);         // read-only: current stored state
```

### `SyncAsync`

1. Resolve the user.
2. **Validate** the request (below).
3. Load the user's stored rows into a `SavedPaycheckSet`.
4. `SavedPaycheckMerger.Merge(existing, incoming)` — the identical algorithm covered in
   [10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md#savedpaycheckmerger--deterministic-last-write-wins).
5. Persist the merged result.
6. Return it as a `SyncResponse`.

### Request validation

```csharp
private const int MaxNameLength = 100;
private const int MaxEntries = 500;

private static bool TryValidate(SyncRequest request, out string error)
{
    if (request.Paychecks.Count + request.Tombstones.Count > MaxEntries) { ... }
    foreach (var name in request.Paychecks.Select(p => p.Name).Concat(request.Tombstones.Select(t => t.Name)))
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength) { ... }
}
```

500 combined entries per sync, 100-character names. `BudgetSyncEndpoints` applies the identical
two limits independently to each of its four collections. A validation failure returns
`Results.ValidationProblem(...)` — an RFC 7807-shaped 400, which is exactly the body shape
`PaycheckApiClient.ReadErrorAsync` knows how to parse into a human-readable message.

### Persistence — upsert-and-reconcile

```csharp
private static async Task PersistAsync(SyncDbContext db, string userId, SavedPaycheckSet merged, CancellationToken ct)
{
    var rows = await db.SavedPaychecks.Where(r => r.UserId == userId).ToListAsync(ct);
    var byKey = rows.ToDictionary(r => r.NameKey, StringComparer.Ordinal);
    var keep = new HashSet<string>(StringComparer.Ordinal);

    void Upsert(string name, DateTimeOffset timestamp, bool isDeleted, string payload)
    {
        var key = name.ToLowerInvariant();
        keep.Add(key);
        if (byKey.TryGetValue(key, out var row)) { row.Name = name; row.UpdatedAtUtc = timestamp; row.IsDeleted = isDeleted; row.PayloadJson = payload; }
        else db.SavedPaychecks.Add(new SavedPaycheckEntity { UserId = userId, NameKey = key, Name = name, UpdatedAtUtc = timestamp, IsDeleted = isDeleted, PayloadJson = payload });
    }

    foreach (var entry in merged.Paychecks) Upsert(entry.Name, entry.UpdatedAtUtc, isDeleted: false, JsonSerializer.Serialize(entry, PaycheckJson.Options));
    foreach (var tomb  in merged.Tombstones) Upsert(tomb.Name, tomb.DeletedAtUtc, isDeleted: true, string.Empty);

    // The merge is a union of keys, so this never drops anything today; kept for safety.
    foreach (var row in rows) if (!keep.Contains(row.NameKey)) db.SavedPaychecks.Remove(row);

    await db.SaveChangesAsync(ct);
}
```

Both a live entry and a tombstone become **the same kind of row** — `SavedPaycheckEntity` with
`IsDeleted` flipped, `PayloadJson` empty for a tombstone. This is a deliberate simplification:
one table, one primary key shape, no separate tombstone table to keep in sync.

`BudgetSyncEndpoints.SyncAsync` runs the identical pattern four times over — once per collection
— merging with `BudgetMerger`, persisting with the matching `Upsert*`/`keep`-set logic, in a
single request/response round trip.

---

## Data layer

### `SyncDbContext`

```csharp
public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<SavedPaycheckEntity> SavedPaychecks => Set<SavedPaycheckEntity>();
    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<BudgetTransactionEntity> BudgetTransactions => Set<BudgetTransactionEntity>();
    public DbSet<RecurringBillEntity> RecurringBills => Set<RecurringBillEntity>();
    public DbSet<SavingsGoalEntity> SavingsGoals => Set<SavingsGoalEntity>();
}
```

Inherits `IdentityDbContext<IdentityUser>` — Identity's own tables (`AspNetUsers`, roles, tokens,
etc.) live in the same database as the sync data, one context, one connection string.

### Entities — the payload-blob pattern

Every synced entity follows the same shape: a **composite key that encodes the sync identity**,
metadata columns, and a `PayloadJson` string:

| Entity | Key | Payload DTO |
|---|---|---|
| `SavedPaycheckEntity` | `(UserId, NameKey)` — `NameKey` = lowercase `Name` | `SavedPaycheckDto` |
| `BudgetEntity` | `(UserId, NameKey)` | `BudgetDto` |
| `BudgetTransactionEntity` | `(UserId, Id)` — `Id` a client-assigned GUID | `TransactionDto` |
| `RecurringBillEntity` | `(UserId, Id)` | `RecurringBillDto` |
| `SavingsGoalEntity` | `(UserId, Id)` | `SavingsGoalDto` |

```csharp
public sealed class SavedPaycheckEntity
{
    public required string UserId { get; set; }
    public required string NameKey { get; set; }    // lower-invariant identity
    public required string Name { get; set; }        // display casing preserved separately
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public string PayloadJson { get; set; } = "";     // serialized DTO for live rows; "" for tombstones
}
```

The whole domain object is stored as **one opaque JSON column**, not normalized into relational
columns. This is why adding a new field to `PaycheckInput` or `Deduction` needs **no EF
migration at all** — it just changes what's inside the blob. A migration is only needed when the
*envelope* changes — a new synced collection, a new key field, a new indexed metadata column.
`NameKey` is kept as a **separate stored column** specifically so the composite primary key can
be a plain string comparison rather than requiring a computed/lowercased index expression.

`OnModelCreating` in `SyncDbContext` configures each entity's composite key and required columns
explicitly; there is no attribute-based configuration on the entity classes themselves.

### Migrations

Three so far, chronologically:

| Migration | Adds |
|---|---|
| `20260614051721_InitialCreate` | Identity schema + `SavedPaycheckEntity` |
| `20260615000000_AddBudgets` | `BudgetEntity` |
| `20260615085709_AddRecurringBillsAndSavingsGoals` | `BudgetTransactionEntity`, `RecurringBillEntity`, `SavingsGoalEntity` |

Applied automatically at startup against PostgreSQL via `db.Database.Migrate()`.

### `SyncDbContextDesignTimeFactory`

```csharp
public sealed class SyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    public SyncDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sync")
            ?? "Host=localhost;Port=5432;Database=paycheckcalculator_dev;Username=admin;Password=password";
        return new SyncDbContext(new DbContextOptionsBuilder<SyncDbContext>().UseNpgsql(connectionString).Options);
    }
}
```

Its presence changes how `dotnet ef` behaves: EF builds the context from **this factory**
instead of executing `Program.cs`, so scaffolding a new migration never triggers the app's
startup database calls (the migrate/`EnsureCreated` block, Identity wiring, etc.). Because
scaffolding only needs the model and the provider — not a live connection — the connection string
just needs to be well-formed; it mirrors `Program.cs`'s default and can be overridden with the
same `ConnectionStrings__Sync` environment variable used at runtime.

---

## PostgreSQL vs. SQLite

| | Production | Integration tests |
|---|---|---|
| Provider | `Npgsql.EntityFrameworkCore.PostgreSQL` | `Microsoft.EntityFrameworkCore.Sqlite` |
| Schema creation | `db.Database.Migrate()` | `db.Database.EnsureCreated()` |
| Connection | `ConnectionStrings:Sync` config, or the hardcoded local default | Shared in-memory-ish SQLite connection kept open for the fixture's lifetime |
| Local dev | [`compose.yml`](../../compose.yml) — `postgres:18` on port `5432`, seeded `paycheckcalculator_dev` DB (user `admin`) | — |

`SyncApiTest` uses `WebApplicationFactory<Program>` with a custom `ApiFactory : WebApplicationFactory<Program>`
that swaps `SyncDbContext`'s registration to point at a shared SQLite connection instead of
Npgsql, so the whole HTTP pipeline — Identity register/login, bearer-token authorization, the
merge, tombstone propagation — is exercised for real, over real HTTP, with no PostgreSQL
dependency in CI.

---

## Running it

```bash
dotnet run --project PaycheckCalculator.API   # defaults to http://localhost:5201
```

`Properties/launchSettings.json` defines `http` (port 5201) and `https` (7201/5201) profiles,
both forcing `ASPNETCORE_ENVIRONMENT=Development`.

For the container/Replit environment, [`start-api.sh`](../../start-api.sh) instead binds
`http://127.0.0.1:5201` explicitly and builds `ConnectionStrings__Sync` from the standard `PG*`
environment variables with `SSL Mode=Disable`. [`.replit`](../../.replit) runs the API and the
Blazor app as parallel workflow tasks so both come up together.

To exercise account/sync end to end, run the API and Blazor together — the Blazor app talks to
the API **server-side, from the circuit**, so there is no browser CORS concern.

---

**Next:** [12 — The Blazor Web App](12-blazor-web-app.md)
