# Getting Started

This page covers the prerequisites, layout, and commands needed to build, test, and run PaycheckCalc.

---

## Prerequisites

- **.NET 11 SDK preview** pinned in [`global.json`](../../global.json): `11.0.100-preview.5.26302.115`, `latestPatch` roll-forward, prerelease allowed.
- **.NET MAUI workload** only when building or running `PaycheckCalc.App`:
  ```bash
  dotnet workload install maui
  ```
- **Android SDK** or **Windows 10+ SDK** depending on the MAUI target platform.
- **PostgreSQL** only when running `PaycheckCalc.Api` with its default provider. The integration tests use a SQLite-backed test path and do not require PostgreSQL.

`PaycheckCalc.Core`, `PaycheckCalc.Shared`, `PaycheckCalc.Api`, `PaycheckCalc.Blazor`, and `PaycheckCalc.Tests` build without the MAUI workload. `PaycheckCalc.App` requires the MAUI workload.

`PaycheckCalc.Core` multi-targets `net11.0;net9.0` when the .NET 11 SDK is available and falls back to `net9.0` on older SDKs. The other non-MAUI projects target `net11.0`. The MAUI app targets `net11.0-android` and `net11.0-windows10.0.19041.0`.

---

## Repository Layout

```text
PaycheckCalc.slnx
├── global.json                   # .NET 11 preview SDK pin
├── PaycheckCalc.Core/            # UI-agnostic tax, pay, gross-up, projection, budget, and report engines
├── PaycheckCalc.App/             # .NET MAUI frontend for Android and Windows
├── PaycheckCalc.Blazor/          # Blazor Server web frontend
├── PaycheckCalc.Shared/          # Sync contracts, JSON config, mergers, API client, stores, entitlements
├── PaycheckCalc.Api/             # ASP.NET Core Web API for Identity accounts and sync
├── PaycheckCalc.Tests/           # xUnit tests for Core, Shared, Api, and Blazor export paths
└── docs/                         # Wiki and Mermaid class diagrams
```

The solution file includes all six projects:

```text
PaycheckCalc.Api
PaycheckCalc.App
PaycheckCalc.Blazor
PaycheckCalc.Core
PaycheckCalc.Shared
PaycheckCalc.Tests
```

---

## Build

### Build a single non-MAUI project

```bash
dotnet build PaycheckCalc.Core
dotnet build PaycheckCalc.Shared
dotnet build PaycheckCalc.Blazor
dotnet build PaycheckCalc.Api
dotnet build PaycheckCalc.Tests
```

### Build the full solution

```bash
dotnet build PaycheckCalc.slnx
```

The full solution build includes `PaycheckCalc.App`, so it requires the MAUI workload and a compatible platform SDK.

---

## Run Tests

```bash
dotnet test PaycheckCalc.Tests
```

The test suite covers federal withholding, FICA, all state calculators, dynamic state schemas, gross-up, annual projection, budgeting, recurring bills, savings goals, budget reports, snapshot JSON round-trips, deterministic merge behavior, sync API integration, and CSV/PDF export renderers.

CI (`.github/workflows/dotnet.yml`) restores, builds, and tests `PaycheckCalc.Tests` on Linux with the pinned .NET 11 preview SDK. CodeQL runs separately.

---

## Run the Apps

### Blazor web app

```bash
dotnet run --project PaycheckCalc.Blazor
```

The Blazor app does not require the MAUI workload. It reads tax data from a `TaxData/` folder copied into the build output.

### Sync API

```bash
dotnet run --project PaycheckCalc.Api
```

The API defaults to `http://localhost:5201` and reads its database connection from `ConnectionStrings:Sync`:

```text
Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres
```

When running against PostgreSQL, EF Core migrations are applied at startup. Run the API and Blazor app together for manual end-to-end account/sync testing.

### MAUI app

```bash
dotnet build PaycheckCalc.App
```

Target-specific run examples:

```bash
# Android
dotnet build PaycheckCalc.App -t:Run -f net11.0-android

# Windows
dotnet build PaycheckCalc.App -t:Run -f net11.0-windows10.0.19041.0
```

The MAUI app packages tax JSON as `MauiAsset` files and reads them through `MauiAppPackageTaxDataReader`.

---

## Project Dependencies

```text
PaycheckCalc.Core     →  no project references
PaycheckCalc.Shared   →  PaycheckCalc.Core
PaycheckCalc.App      →  PaycheckCalc.Core, PaycheckCalc.Shared
PaycheckCalc.Blazor   →  PaycheckCalc.Core, PaycheckCalc.Shared
PaycheckCalc.Api      →  PaycheckCalc.Shared
PaycheckCalc.Tests    →  PaycheckCalc.Core, PaycheckCalc.Shared, PaycheckCalc.Api, PaycheckCalc.Blazor
```

Layering rules:

- `PaycheckCalc.Core` has no UI, HTTP, MAUI, database, or persistence dependency.
- `PaycheckCalc.Shared` references Core and owns DTOs, JSON serialization, deterministic mergers, API client code, store abstractions, and entitlement abstractions.
- `PaycheckCalc.Api` references Shared and persists sync state with EF Core/PostgreSQL.
- Front-ends reference Core and Shared, not each other.

---

## JSON Tax Data

Tax data lives in [`PaycheckCalc.Core/Data/`](../../PaycheckCalc.Core/Data/):

| File | Description |
|---|---|
| `us_irs_15t_2026_percentage_automated.json` | IRS Publication 15-T 2026 percentage-method data |
| `ar_withholding_2026.json` | Arkansas withholding tables |
| `ca_method_b_2026.json` | California Method B withholding data |
| `co_dr0004_2026.json` | Colorado DR 0004 Table 1 allowance data |
| `connecticut_withholding_2026.json` | Connecticut withholding tables |
| `ok_ow2_2026_percentage.json` | Oklahoma OW-2 withholding tables |
| `state_supplemental_2026.json` | State supplemental (bonus) withholding rates for all 50 states + DC |
| `Schemas/*.json` | Dynamic state-input schema files for every state / DC |

`AddPaycheckCalcCore` reads the JSON files at startup and registers the corresponding calculators and schema provider. If a JSON file is renamed or moved, update all consumers: Core loader, MAUI assets, Blazor `TaxData` links, test project links, and any tests or documentation that reference the old name.
