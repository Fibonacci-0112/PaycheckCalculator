# Accounts & Paycheck Sync

Saved paychecks can optionally sync between the MAUI app and the Blazor web app through a user
account. Accounts are **entirely optional** — both front-ends work fully without one.

- **MAUI app:** saved paychecks are persisted **on the device** (a JSON file in app data), so they
  survive restarts with or without an account.
- **Blazor app:** anonymous saved paychecks live **only until the browser tab closes** (they are held
  in circuit memory). Signing in syncs them to the server, which retains them across sessions.
- **With an account:** signing in on either client merges and syncs the same set of paychecks.

---

## Projects

| Project | Role |
|---|---|
| `PaycheckCalc.Shared` | Wire/storage contracts, JSON serialization, the merge rule, the typed HTTP client, and the `ISavedPaycheckStore` abstraction. References `PaycheckCalc.Core` only. |
| `PaycheckCalc.Api` | Standalone ASP.NET Core Web API: ASP.NET Core Identity accounts (email/password, bearer tokens) over EF Core SQLite, plus the `/api/paychecks/sync` endpoint. |

Both front-ends call the API over HTTP via the shared `PaycheckApiClient`. The Blazor app makes these
calls **server-side** (from its circuit), so no CORS configuration is needed.

---

## Request flow

```
MAUI app  ─┐                         ┌─ /api/account/{register,login,refresh}  (ASP.NET Core Identity)
           ├─ PaycheckApiClient ──▶  │
Blazor app ─┘   (bearer token)       └─ /api/paychecks/sync  ──▶  SavedPaycheckMerger ──▶  SQLite
```

1. The client loads its local `SavedPaycheckSet` (live entries + delete tombstones).
2. It POSTs that set to `/api/paychecks/sync` with a bearer token.
3. The server merges the pushed set with the user's stored set using `SavedPaycheckMerger`, persists the
   result, and returns the full merged set.
4. The client replaces its local state with the merged set (`ISavedPaycheckStore.ReplaceAllAsync`).

Because the merge runs server-side and the client replaces, clients need no conflict-resolution logic.

---

## Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/account/register` | none | Create an account (email + password). |
| `POST` | `/api/account/login` | none | Get a bearer access token + refresh token. |
| `POST` | `/api/account/refresh` | none | Exchange a refresh token for a new access token. |
| `POST` | `/api/paychecks/sync` | bearer | Push a `SyncRequest`, merge, and return the merged `SyncResponse`. |
| `GET` | `/api/paychecks/` | bearer | Pull the stored set without writing. |

Account endpoints are the standard `MapIdentityApi<IdentityUser>` endpoints. The default Identity
password policy applies (≥ 6 chars with upper, lower, digit, and non-alphanumeric). Email confirmation is
**not** required (no email sender is configured), so register → login works immediately. Access tokens
last ~1 hour; the client transparently refreshes once on a `401` before retrying a sync.

---

## Snapshot model

A saved paycheck (`SavedPaycheckDto`) carries:

- `Name` — the user-facing label and the **case-insensitive identity** used for upsert/merge.
- `UpdatedAtUtc` — drives last-write-wins.
- `SchemaVersion` — currently `1`, for forward-compatible migrations.
- `Input` — the full domain `PaycheckInput` that produced the result (so it can be reloaded later).
- `Result` — flattened result numbers (`SavedPaycheckResultDto`) for display. The "Show Your Work"
  explanation is **not** stored (it is regenerated on demand).

Serialization uses one shared configuration (`PaycheckJson.Options`): Web defaults, enums as **names**
(never ordinals), and a custom `StateInputValuesJsonConverter` that materializes the dynamic
`StateInputValues` bag as real CLR primitives (`string`/`bool`/`int`/`decimal`) rather than
`JsonElement`, so the state calculators' `GetValueOrDefault<T>` keeps working after a round-trip.

### Merge semantics (last-write-wins)

Per name key (lower-invariant), `SavedPaycheckMerger` picks the winner:

1. later timestamp wins;
2. on an exact timestamp tie, a live entry beats a delete tombstone;
3. on a tie between two of the same kind, the incoming side wins (idempotent re-sync).

Deletes are recorded as **tombstones** (kept even for anonymous users) so a delete propagates to other
devices on the next sync instead of being resurrected by a stale copy.

---

## Storage per client

| Client | Backing store | Lifetime |
|---|---|---|
| MAUI | `JsonFilePaycheckStore` → `saved-paychecks.json` in `FileSystem.AppDataDirectory` | Durable on device. Tokens in `SecureStorage` (falls back to `Preferences` on unpackaged Windows). |
| Blazor | `SessionPaycheckStore` (in-memory) | Circuit-scoped — discarded when the tab closes. Tokens in circuit memory only. |
| Server | `SavedPaycheckEntity` rows in SQLite, keyed by `(UserId, NameKey)` | Persistent. |

The MAUI server URL is editable on the Account page (stored in `Preferences`, default
`http://localhost:5201`). From the Android emulator, the host machine is reachable at `http://10.0.2.2:5201`.

---

## Database

`SyncDbContext : IdentityDbContext<IdentityUser>` holds the Identity tables plus `SavedPaychecks`. The
schema is created at startup with `Database.EnsureCreated()` rather than EF migrations. This keeps the
greenfield store simple and tooling-free and works identically under in-memory SQLite in the integration
tests. **Trade-off:** there is no migration path — a schema change requires recreating the database.

The connection string comes from `ConnectionStrings:Sync` (default `Data Source=paycheckcalc-sync.db`).
Runtime `*.db` files are git-ignored.

---

## Known limitations

- **Device clock skew** affects last-write-wins, since timestamps are set by the writing client.
- **No "reload into form" UI yet.** The full `PaycheckInput` is stored, but reloading a saved paycheck
  back into the calculator form is a future enhancement.
- **Cleartext HTTP on Android.** The dev default is plain `http://`. Android blocks cleartext to
  non-localhost hosts by default on API 28+; use an `https://` server or add a network security config.
- **No server-side rate limiting** on the account or sync endpoints.

---

## Testing

- `SavedPaycheckMergerTest` and `PaycheckSnapshotJsonTest` (unit) cover the merge rules and the JSON
  round-trip, including a calculation-equivalence check.
- `SyncApiTest` (integration, `WebApplicationFactory` + in-memory SQLite) covers register/login, `401`
  without a token, last-write-wins across two clients, tombstone propagation, and a full
  `StateInputValues` server round-trip.

Run the API and Blazor together for a manual end-to-end check:

```bash
dotnet run --project PaycheckCalc.Api      # http://localhost:5201
dotnet run --project PaycheckCalc.Blazor   # then sign in from the "Saved Paychecks & Account" panel
```
