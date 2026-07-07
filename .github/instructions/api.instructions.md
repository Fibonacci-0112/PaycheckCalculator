---
applyTo: "PaycheckCalc.Api/**/*.cs"
---

# API project instructions

- `PaycheckCalc.Api` is the optional sync server. It provides ASP.NET Core Identity email/password accounts and minimal API endpoints for syncing saved paychecks, budgets, transactions, recurring bills, and savings goals.
- Do not reference `PaycheckCalc.App`, `PaycheckCalc.Blazor`, or any client UI model from this project.
- Do not duplicate DTOs, merge algorithms, or serialization logic already defined in `PaycheckCalc.Shared`. Use Shared types directly.
- Do not put calculation logic in endpoint handlers. The API syncs stored results; Core remains the calculation engine.

## Endpoint design

- Keep handlers small: authenticate, validate, load rows, call Shared merge/serialization logic, persist, return DTOs.
- All user sync endpoints must require authentication. Do not introduce unauthenticated access to user data unless explicitly required by the task.
- Request/response contracts must stay compatible with the MAUI and Blazor clients. Use `PaycheckJson.AddConverters` on the minimal-API JSON pipeline so `StateInputValues` and enums round-trip correctly.
- Do not log passwords, bearer tokens, serialized paycheck details, or any other sensitive account data.

## EF Core and database

- Production targets PostgreSQL via Npgsql. Test paths use SQLite + `EnsureCreated` — do not introduce SQLite-only behavior in production code paths.
- When entity shape changes, generate a migration through EF tooling rather than hand-editing migration files or the snapshot, except for obvious generated-code repairs.
- Keep EF Core and Npgsql package versions consistent with the pinned preview line already in the project file.
- Apply migrations at startup via `db.Database.Migrate()` only on the Npgsql path; tests use `EnsureCreated`.

## Testing

- Add or update integration tests in `PaycheckCalc.Tests` for new or changed endpoint behavior, auth requirements, merge persistence, tombstones, and budget sync.
- Use the existing test host / in-memory or SQLite-backed paths. Do not require a developer PostgreSQL instance for tests to pass.
