---
applyTo: "PaycheckCalculator.Shared/**/*.cs"
---

# Shared library instructions

- `PaycheckCalculator.Shared` is the cross-client contract and sync layer. It may reference `PaycheckCalculator.Core` but must not reference MAUI, Blazor, ASP.NET Core hosting, EF Core, or platform storage APIs.
- Do not put calculation logic here. Core is the calculation engine; Shared only syncs stored results and state.

## DTOs and JSON

- Treat all DTO changes as contract changes that affect MAUI, Blazor, the API, and tests simultaneously. Prefer additive fields with safe defaults over breaking renames or removals.
- `StateInputValues` must round-trip as real CLR primitive values. `StateInputValuesJsonConverter` enforces this — never allow `JsonElement` to leak into consumers.
- Preserve `PaycheckJson` behavior and all custom converters. When adding a new converter or changing serialization, update all corresponding readers/writers and add round-trip tests.
- Keep enum and `DateOnly` handling stable across all consumers.

## Merge and sync logic

- Merge logic must be deterministic and side-effect-free. Saved paycheck names are compared case-insensitively; tombstones win according to last-write-wins rules.
- `SavedPaycheckMerger` and `BudgetMerger` are defined once here and reused by the API and both clients. Do not duplicate merge behavior in consuming projects.
- Budget sync has separate sets for budgets, transactions, recurring bills, and savings goals. Preserve their identity rules and tombstone behavior independently.
- Sync service classes (`PaycheckSyncService`, `BudgetSyncService`) orchestrate local store + API client + merger. They must not calculate paychecks, render UI, or perform direct HTTP I/O beyond the typed client.

## Abstractions

- Keep persistence abstractions (`ISavedPaycheckStore`, `IBudgetStore`) here; concrete implementations belong in the consuming projects (MAUI/Blazor) or the API.
- Keep HTTP transport isolated in `PaycheckApiClient` and the `ITokenStore`/`IApiBaseAddressProvider` abstractions. Do not leak server or UI implementation details into client contracts.
- `IEntitlementProvider` and its implementations belong here. Keep entitlement checks lightweight and side-effect-free.
