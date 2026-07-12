# AGENTS.md - PaycheckCalculator.Shared

Scope: this file applies to everything under `PaycheckCalculator.Shared/`.

## Role of this project

`PaycheckCalculator.Shared` is the cross-client sync and contract layer. It owns wire/storage DTOs, JSON options and converters, deterministic merge logic, API client abstractions, token/base-address abstractions, sync orchestration, and entitlement contracts used by MAUI, Blazor, the API, and tests.

## Dependency boundaries

- This project may reference Core, but it must not reference MAUI, Blazor, ASP.NET Core hosting, EF Core, platform storage APIs, or UI presentation models.
- Keep persistence abstractions here; concrete storage belongs in the consuming project or API.
- Keep HTTP transport isolated in the typed API client and abstractions. Do not leak UI or server implementation details into DTOs.

## JSON and compatibility rules

- Preserve `PaycheckJson` behavior and all custom converters unless the corresponding readers/writers/tests are updated together.
- `StateInputValues` must round-trip as real CLR primitive values; do not allow `JsonElement` to leak into consumers.
- Treat DTO changes as contract changes. Prefer additive fields with safe defaults over breaking renames/removals.
- Keep enum/date handling stable across MAUI, Blazor, API, and tests.

## Merge and sync rules

- Merge logic must remain deterministic and side-effect free.
- Saved paycheck names are compared case-insensitively. Tombstones must continue to win according to existing last-write-wins rules.
- Budget sync has separate sets for budgets, transactions, recurring bills, and savings goals. Preserve their identity rules and tombstone behavior.
- Sync services should orchestrate local store + API client + merger. They should not calculate paychecks or render UI.

## Testing expectations

- Add/update tests in `PaycheckCalculator.Tests` for JSON contract changes, merge edge cases, tombstone handling, API client assumptions, and sync service behavior.
- Include round-trip tests when adding DTO fields or converters.

## Useful commands

```bash
dotnet build PaycheckCalculator.Shared
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~Json|FullyQualifiedName~Merger|FullyQualifiedName~Sync"
```

This project targets `net11.0` and should build without MAUI or the API runtime.
