# Accounts & Sync

Saved paychecks and budget data can optionally sync between the MAUI app and the Blazor web app through a user account.

Accounts are optional. Both front-ends work without an account:

- **MAUI app:** saved paychecks and budgets persist locally on the device.
- **Blazor app:** anonymous saved paychecks and budgets live in circuit memory for the browser session.
- **With an account:** either client can push local state to the API, receive the merged state, and replace its local state.

---

## Projects

| Project | Role |
|---|---|
| `PaycheckCalc.Shared` | DTOs, JSON options, merge rules, typed API client, store abstractions, sync services, and entitlement abstractions. |
| `PaycheckCalc.Api` | ASP.NET Core Web API with Identity accounts, EF Core PostgreSQL persistence, and sync endpoints. |
| `PaycheckCalc.App` | Local device stores plus account/sync UI. |
| `PaycheckCalc.Blazor` | Circuit-scoped stores plus account/sync UI. |

`PaycheckCalc.Shared` references Core only. `PaycheckCalc.Api` references Shared and does not reference the front-end projects.

---

## Request Flow

```text
MAUI app  ─┐                         ┌─ /api/account/{register,login,refresh}
           ├─ PaycheckApiClient ──▶  │─ /api/paychecks/sync
Blazor app ─┘                         └─ /api/budgets/sync
                                           ↓
                                    Shared merger logic
                                           ↓
                                      PostgreSQL rows
```

General sync sequence:

1. Client loads its local set.
2. Client posts the local set to the matching `/sync` endpoint.
3. Server loads the stored set for the account.
4. Server merges the client set and server set with the shared merger.
5. Server persists the merged state.
6. Server returns the full merged state.
7. Client replaces local state with the returned state.

Clients do not implement separate conflict-resolution logic; they reuse the shared sync services and accept the server's merged result.

---

## Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/account/register` | None | Create an account. |
| `POST` | `/api/account/login` | None | Sign in and receive Identity bearer credentials. |
| `POST` | `/api/account/refresh` | None | Refresh the Identity session. |
| `POST` | `/api/paychecks/sync` | Required | Push saved paycheck state, merge, return merged state. |
| `GET` | `/api/paychecks/` | Required | Return saved paycheck state without writing. |
| `POST` | `/api/budgets/sync` | Required | Push budget, transaction, recurring bill, and savings goal state, merge, return merged state. |
| `GET` | `/api/budgets/` | Required | Return budget-related state without writing. |

Account endpoints are mapped through ASP.NET Core Identity. The sync endpoints validate collection sizes and budget names before merging.

---

## Saved Paycheck Snapshot Model

A saved paycheck carries:

- `Name` — user-facing label and case-insensitive merge identity.
- `UpdatedAtUtc` — conflict-resolution timestamp.
- `SchemaVersion` — forward-compatible schema marker.
- `Input` — full `PaycheckInput` used to produce the result.
- `Result` — flattened result numbers, including gross-up fields when applicable.

The show-your-work explanation is not stored; it is regenerated from the saved input when needed.

---

## Budget Sync Model

Budget sync is split into four independent collections.

| Collection | DTO | Removal record | Identity |
|---|---|---|---|
| Budgets | `BudgetDto` | `BudgetTombstone` | Case-insensitive budget name |
| Transactions | `TransactionDto` | `TransactionTombstone` | GUID |
| Recurring bills | `RecurringBillDto` | `RecurringBillTombstone` | GUID |
| Savings goals | `SavingsGoalDto` | `SavingsGoalTombstone` | GUID |

`BudgetDto` is currently schema version 2 and includes the budget method. Recurring bills and savings goals sync as separate GUID-keyed sets instead of being embedded inside the budget DTO.

`BudgetSyncRequest` carries all four live collections plus their removal records. `BudgetSyncResponse` returns the merged state for all four collections plus server time.

---

## JSON Configuration

Serialization uses `PaycheckJson.Options` from Shared:

- Web-style JSON defaults.
- Enums serialized as names, not ordinals.
- `DateOnlyJsonConverter` for budget transaction and savings goal dates.
- `StateInputValuesJsonConverter` so dynamic state inputs round-trip as concrete CLR values instead of `JsonElement` values.

The same JSON options are used by clients and the API minimal-API JSON pipeline.

---

## Merge Semantics

Saved paychecks and budget-related collections use deterministic last-write-wins behavior:

1. Later timestamp wins.
2. On exact timestamp ties, a live entry beats a removal record.
3. On exact same-kind ties, the incoming side wins.

Identity keys:

| Data | Identity |
|---|---|
| Saved paychecks | Case-insensitive name |
| Budgets | Case-insensitive name |
| Transactions | GUID |
| Recurring bills | GUID |
| Savings goals | GUID |

Removal records are retained so removals propagate to other devices during later syncs.

---

## Storage by Client

| Client | Backing store | Lifetime |
|---|---|---|
| MAUI | `JsonFilePaycheckStore` and `JsonFileBudgetStore` in app data | Durable on device |
| Blazor | `SessionPaycheckStore` and `SessionBudgetStore` | Circuit-scoped |
| Server | EF Core rows in PostgreSQL | Persistent |

The MAUI Account page exposes the sync server URL. The default local API URL is `http://localhost:5201`; Android emulator access to the host machine commonly uses `http://10.0.2.2:5201`.

The Blazor app reads its API base URL from configuration through `ConfigApiBaseAddressProvider`.

---

## Database

`SyncDbContext` derives from `IdentityDbContext<IdentityUser>` and contains Identity tables plus sync tables.

Current sync entity sets:

- `SavedPaycheckEntity`
- `BudgetEntity`
- `BudgetTransactionEntity`
- `RecurringBillEntity`
- `SavingsGoalEntity`

Production-style runs use PostgreSQL through Npgsql. The connection string is `ConnectionStrings:Sync`, with environment-variable override support through standard .NET configuration.

At startup:

- PostgreSQL uses EF Core migrations via `Database.Migrate()`.
- Non-PostgreSQL test paths use `EnsureCreated()`.

`SyncDbContextDesignTimeFactory` supports EF CLI migration creation.

---

## Known Limitations

- Last-write-wins uses client-provided timestamps, so device clock skew can affect conflict resolution.
- Server-side rate limiting is not currently implemented.
- Development defaults use plain local HTTP; production deployment should use HTTPS.
- Budget reports are wired in Core/UI/export paths but gated by the entitlement provider until a paid entitlement implementation is added.

---

## Testing

Relevant tests include:

- Saved paycheck merge and JSON round-trip tests.
- Budget merger tests across budgets, transactions, recurring bills, and savings goals.
- Sync API integration tests using `WebApplicationFactory`.
- Budget calculator and report calculator tests.

Manual end-to-end check:

```bash
dotnet run --project PaycheckCalc.Api
dotnet run --project PaycheckCalc.Blazor
```

Then sign in from the web app's account area and verify paycheck/budget sync.
