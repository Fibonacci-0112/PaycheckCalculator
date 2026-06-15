# Accounts & Sync

Saved **paychecks** and **budgets** can optionally sync between the MAUI app and the Blazor web app
through a user account. Accounts are **entirely optional** — both front-ends work fully without one.

- **MAUI app:** saved paychecks and budgets are persisted **on the device** (JSON files in app data), so
  they survive restarts with or without an account.
- **Blazor app:** anonymous saved paychecks and budgets live **only until the browser tab closes** (they
  are held in circuit memory). Signing in syncs them to the server, which retains them across sessions.
- **With an account:** signing in on either client merges and syncs the same data.

---

## Projects

| Project | Role |
|---|---|
| `PaycheckCalc.Shared` | Wire/storage contracts, JSON serialization, the merge rules, the typed HTTP client, and the `ISavedPaycheckStore` / `IBudgetStore` abstractions. References `PaycheckCalc.Core` only. |
| `PaycheckCalc.Api` | Standalone ASP.NET Core Web API: ASP.NET Core Identity accounts (email/password, bearer tokens) over EF Core **PostgreSQL**, plus the `/api/paychecks/sync` and `/api/budgets/sync` endpoints. |

Both front-ends call the API over HTTP via the shared `PaycheckApiClient`. The Blazor app makes these
calls **server-side** (from its circuit), so no CORS configuration is needed.

---

## Request flow

```
MAUI app  ─┐                         ┌─ /api/account/{register,login,refresh}  (ASP.NET Core Identity)
           ├─ PaycheckApiClient ──▶  │─ /api/paychecks/sync  ──▶  SavedPaycheckMerger ─┐
Blazor app ─┘   (bearer token)       └─ /api/budgets/sync    ──▶  BudgetMerger ────────┴─▶ PostgreSQL
```

1. The client loads its local set (live entries + delete tombstones).
2. It POSTs that set to `/api/paychecks/sync` (or `/api/budgets/sync`) with a bearer token.
3. The server merges the pushed set with the user's stored set using the shared merger, persists the
   result, and returns the full merged set.
4. The client replaces its local state with the merged set (`ReplaceAll…Async`).

Because the merge runs server-side and the client replaces, clients need no conflict-resolution logic.

---

## Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/account/register` | none | Create an account (email + password). |
| `POST` | `/api/account/login` | none | Get a bearer access token + refresh token. |
| `POST` | `/api/account/refresh` | none | Exchange a refresh token for a new access token. |
| `POST` | `/api/paychecks/sync` | bearer | Push a `SyncRequest`, merge, and return the merged `SyncResponse`. |
| `GET` | `/api/paychecks/` | bearer | Pull the stored paycheck set without writing. |
| `POST` | `/api/budgets/sync` | bearer | Push a `BudgetSyncRequest` (budgets + transactions), merge, and return the merged `BudgetSyncResponse`. |
| `GET` | `/api/budgets/` | bearer | Pull the stored budgets + transactions without writing. |

Account endpoints are the standard `MapIdentityApi<IdentityUser>` endpoints. The default Identity password
policy applies (≥ 6 chars with upper, lower, digit, and non-alphanumeric). Email confirmation is **not**
required (no email sender is configured), so register → login works immediately. Access tokens last
~1 hour; the client transparently refreshes once on a `401` before retrying. The sync endpoints validate
input: at most **500** entries per collection and names **1–100** characters.

---

## Snapshot model

### Paychecks

A saved paycheck (`SavedPaycheckDto`) carries:

- `Name` — the user-facing label and the **case-insensitive identity** used for upsert/merge.
- `UpdatedAtUtc` — drives last-write-wins.
- `SchemaVersion` — currently `1`, for forward-compatible migrations.
- `Input` — the full domain `PaycheckInput` that produced the result (so it can be reloaded later).
- `Result` — flattened result numbers (`SavedPaycheckResultDto`, built by `SavedPaycheckResultMapper`),
  including gross-up fields (`IsGrossUp`, `TargetNetPay`, `GrossUpCost`). The "Show Your Work" explanation
  is **not** stored (it is regenerated on demand).

### Budgets & transactions

- `BudgetDto` — `Name` (case-insensitive identity), `UpdatedAtUtc`, `SchemaVersion`, `MonthlyNetIncome`,
  and a list of `BudgetCategoryDto` (name, type, amount, dollar/percentage).
- `TransactionDto` — a stable `Id` (GUID, the merge key), `BudgetName`, `CategoryName`, `Amount`, `Date`
  (a `DateOnly`, serialized ISO-8601 via `DateOnlyJsonConverter`), `Description`, and `UpdatedAtUtc`.

Serialization uses one shared configuration (`PaycheckJson.Options`): Web defaults, enums as **names**
(never ordinals), a custom `StateInputValuesJsonConverter` that materializes the dynamic `StateInputValues`
bag as real CLR primitives (`string`/`bool`/`int`/`decimal`) rather than `JsonElement`, and the
`DateOnlyJsonConverter` for transaction dates.

### Merge semantics (last-write-wins)

Per identity key, `SavedPaycheckMerger` and `BudgetMerger` pick the winner the same way:

1. later timestamp wins;
2. on an exact timestamp tie, a live entry beats a delete tombstone;
3. on a tie between two of the same kind, the incoming side wins (idempotent re-sync).

Paychecks and budgets are keyed by **case-insensitive name**; transactions are keyed by their **GUID**.
Deletes are recorded as **tombstones** (kept even for anonymous users) so a delete propagates to other
devices on the next sync instead of being resurrected by a stale copy.

---

## Storage per client

| Client | Backing store | Lifetime |
|---|---|---|
| MAUI | `JsonFilePaycheckStore` → `saved-paychecks.json` and `JsonFileBudgetStore` → `budgets.json`, both in `FileSystem.AppDataDirectory` | Durable on device. Tokens in `SecureStorage` (falls back to `Preferences` on unpackaged Windows). |
| Blazor | `SessionPaycheckStore` and `SessionBudgetStore` (in-memory) | Circuit-scoped — discarded when the tab closes. Tokens in circuit memory only (`CircuitAccountSession`). |
| Server | `SavedPaycheckEntity` (PK `UserId,NameKey`), `BudgetEntity` (PK `UserId,NameKey`), and `BudgetTransactionEntity` (PK `UserId,Id`) rows in PostgreSQL | Persistent. Each row stores the serialized DTO in `PayloadJson` (empty for tombstones). |

The MAUI server URL is editable on the Account page (`PreferencesApiBaseAddressProvider`, stored in
`Preferences`, default `http://localhost:5201`). From the Android emulator, the host machine is reachable
at `http://10.0.2.2:5201`. The Blazor app reads its API base address from configuration
(`PaycheckApi:BaseUrl`) via `ConfigApiBaseAddressProvider`.

---

## Database

`SyncDbContext : IdentityDbContext<IdentityUser>` holds the Identity tables plus `SavedPaychecks`,
`Budgets`, and `BudgetTransactions`. Production runs on **PostgreSQL** (Npgsql); the connection string
comes from `ConnectionStrings:Sync` (env override `ConnectionStrings__Sync`), defaulting to
`Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres`.

At startup the app **applies EF Core migrations** when running against PostgreSQL (`Database.Migrate()`) so
the schema can evolve as tables/columns are added (`Migrations/` currently holds `InitialCreate` and
`AddBudgets`). The integration tests swap in **SQLite**, which the Npgsql-targeted migrations don't apply
to, so that path builds the schema directly from the model via `EnsureCreated()` instead. A
`SyncDbContextDesignTimeFactory` lets the EF CLI add new migrations. Runtime `*.db` files are git-ignored.

---

## Known limitations

- **Device clock skew** affects last-write-wins, since timestamps are set by the writing client.
- **No "reload into form" UI yet.** The full `PaycheckInput` is stored, but reloading a saved paycheck back
  into the calculator form is a future enhancement.
- **Cleartext HTTP on Android.** The dev default is plain `http://`. Android blocks cleartext to
  non-localhost hosts by default on API 28+; use an `https://` server or add a network security config.
- **No server-side rate limiting** on the account or sync endpoints.

---

## Testing

- `SavedPaycheckMergerTest`, `BudgetMergerTest`, and `PaycheckSnapshotJsonTest` (unit) cover the merge
  rules and the JSON round-trip, including a calculation-equivalence check.
- `SyncApiTest` (integration, `WebApplicationFactory` + in-memory SQLite) covers register/login, `401`
  without a token, last-write-wins across two clients, tombstone propagation, and a full `StateInputValues`
  server round-trip.

Run the API and Blazor together for a manual end-to-end check (the API needs a reachable PostgreSQL
instance):

```bash
dotnet run --project PaycheckCalc.Api      # http://localhost:5201
dotnet run --project PaycheckCalc.Blazor   # then sign in from the "Saved Paychecks & Account" panel
```
