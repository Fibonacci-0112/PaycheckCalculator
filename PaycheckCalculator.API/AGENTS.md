# AGENTS.md - PaycheckCalculator.API

Scope: this file applies to everything under `PaycheckCalculator.API/`.

## Role of this project

`PaycheckCalculator.API` is the optional sync server. It provides ASP.NET Core Identity email/password accounts and minimal API endpoints for syncing saved paychecks, budgets, transactions, recurring bills, and savings goals. It persists sync state through EF Core and PostgreSQL in normal operation, with tests using a lightweight test configuration.

## Dependency boundaries

- Keep the API as a server adapter around Shared contracts and merge behavior.
- Do not reference MAUI, Blazor, or client UI models from this project.
- Avoid duplicating DTOs or merge algorithms already defined in Shared.
- Do not move calculation logic into API endpoints. The API syncs stored results/state; Core remains the calculation engine.

## Endpoint rules

- Keep endpoint handlers small: authenticate, validate, load rows, call Shared merge/serialization logic, persist, and return DTOs.
- Require authenticated access for user sync data unless a task explicitly introduces a public endpoint.
- Do not log passwords, bearer tokens, serialized paycheck details, or other sensitive account data.
- Keep request/response contracts compatible with MAUI and Blazor clients.

## EF Core rules

- Keep EF Core package versions consistent with the pinned Npgsql-compatible preview line in the project file.
- When entity shape changes, add a migration and update `SyncDbContextModelSnapshot` through normal EF tooling rather than hand-editing snapshots except for obvious generated-code repairs.
- Preserve PostgreSQL compatibility. Do not introduce SQLite-only behavior outside tests.

## Testing expectations

- Add/update `PaycheckCalculator.Tests` integration tests for endpoint behavior, auth requirements, merge persistence, tombstones, and budget sync changes.
- Prefer test host/in-memory or SQLite-backed test paths already used by the suite; do not require a developer PostgreSQL instance for normal tests.

## Useful commands

```bash
dotnet build PaycheckCalculator.API
dotnet run --project PaycheckCalculator.API
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~SyncApi"
```

The API targets `net10.0` and should build without the MAUI workload.
