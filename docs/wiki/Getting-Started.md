# Getting Started

This page covers the prerequisites, layout, and commands needed to build, test, and run PaycheckCalculator.

---

## Prerequisites

- **.NET 11 SDK preview** pinned in [`global.json`](../../global.json): `11.0.100-preview.7.26381.103`, `latestFeature` roll-forward, prerelease allowed.
- **.NET MAUI workload** only when building or running `PaycheckCalculator.App`:
  ```bash
  dotnet workload install maui
  ```
- **Android SDK**, **Xcode on macOS** (for iOS and Mac Catalyst), or **Windows 10+ SDK** depending on the MAUI target platform.
- **PostgreSQL** only when running `PaycheckCalculator.Api` with its default provider. The integration tests use a SQLite-backed test path and do not require PostgreSQL.

`PaycheckCalculator.Core`, `PaycheckCalculator.Shared`, `PaycheckCalculator.Api`, `PaycheckCalculator.Blazor`, and `PaycheckCalculator.Tests` build without the MAUI workload. `PaycheckCalculator.App` requires the MAUI workload.

All non-MAUI projects target `net11.0`. The MAUI app targets `net11.0-android`, `net11.0-ios`, `net11.0-maccatalyst`, and `net11.0-windows10.0.19041.0`; Apple targets are included only when MSBuild runs on macOS, and the Windows target is included only on Windows.

---

## Repository Layout

```text
PaycheckCalculator.slnx
├── global.json                   # .NET 11 preview SDK pin
├── PaycheckCalculator.Core/            # UI-agnostic tax, pay, gross-up, projection, budget, and report engines
├── PaycheckCalculator.App/             # .NET MAUI frontend for Android, iOS, macOS, and Windows
├── PaycheckCalculator.Blazor/          # Blazor Server web frontend
├── PaycheckCalculator.Shared/          # Sync contracts, JSON config, mergers, API client, stores, entitlements
├── PaycheckCalculator.Api/             # ASP.NET Core Web API for Identity accounts and sync
├── PaycheckCalculator.Tests/           # xUnit tests for Core, Shared, Api, and Blazor export paths
└── docs/                         # Wiki and Mermaid class diagrams
```

The solution file includes all six projects:

```text
PaycheckCalculator.Api
PaycheckCalculator.App
PaycheckCalculator.Blazor
PaycheckCalculator.Core
PaycheckCalculator.Shared
PaycheckCalculator.Tests
```

---

## Build

### Build a single non-MAUI project

```bash
dotnet build PaycheckCalculator.Core
dotnet build PaycheckCalculator.Shared
dotnet build PaycheckCalculator.Blazor
dotnet build PaycheckCalculator.Api
dotnet build PaycheckCalculator.Tests
```

### Build the full solution

```bash
dotnet build PaycheckCalculator.slnx
```

The full solution build includes `PaycheckCalculator.App`, so it requires the MAUI workload and a compatible platform SDK.

---

## Run Tests

```bash
dotnet test PaycheckCalculator.Tests
```

The test suite covers federal withholding, FICA, all state calculators, dynamic state schemas, gross-up, annual projection, budgeting, recurring bills, savings goals, budget reports, snapshot JSON round-trips, deterministic merge behavior, sync API integration, and CSV/PDF export renderers.

CI (`.github/workflows/dotnet.yml`) explicitly restores, builds, and tests `PaycheckCalculator.Tests` on Linux with the pinned .NET 11 preview SDK. The test project's references build Core, Shared, API, and Blazor transitively; MAUI is not built by this workflow. CodeQL runs separately.

---

## Run the Apps

### Blazor web app

```bash
dotnet run --project PaycheckCalculator.Blazor
```

The Blazor app does not require the MAUI workload. It reads tax data from a `TaxData/` folder copied into the build output.

### Sync API

```bash
dotnet run --project PaycheckCalculator.Api
```

The API defaults to `http://localhost:5201` and reads its database connection from `ConnectionStrings:Sync`:

```text
Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres
```

For a PostgreSQL container that publishes port `5432` to the host, pass the connection string through
the standard .NET environment-variable form when starting the API:

```bash
ConnectionStrings__Sync='YOUR_POSTGRESQL_CONNECTION_STRING' \
  dotnet run --project PaycheckCalculator.Api
```

Replace the database name, username, and password with the values configured for the container. When
running against PostgreSQL, EF Core migrations are applied at startup. Keep the API running alongside
the MAUI or Blazor client for manual end-to-end account/sync testing.

### MAUI app

```bash
dotnet build PaycheckCalculator.App
```

Target-specific run examples:

```bash
# Android
dotnet build PaycheckCalculator.App -t:Run -f net11.0-android

# iOS (requires macOS and Xcode)
dotnet build PaycheckCalculator.App -t:Run -f net11.0-ios

# macOS via Mac Catalyst (requires macOS and Xcode)
dotnet build PaycheckCalculator.App -t:Run -f net11.0-maccatalyst

# Windows
dotnet build PaycheckCalculator.App -t:Run -f net11.0-windows10.0.19041.0
```

The MAUI app packages tax JSON as `MauiAsset` files and reads them through `MauiAppPackageTaxDataReader`.

---

## Project Dependencies

```text
PaycheckCalculator.Core     →  no project references
PaycheckCalculator.Shared   →  PaycheckCalculator.Core
PaycheckCalculator.App      →  PaycheckCalculator.Core, PaycheckCalculator.Shared
PaycheckCalculator.Blazor   →  PaycheckCalculator.Core, PaycheckCalculator.Shared
PaycheckCalculator.Api      →  PaycheckCalculator.Shared
PaycheckCalculator.Tests    →  PaycheckCalculator.Core, PaycheckCalculator.Shared, PaycheckCalculator.Api, PaycheckCalculator.Blazor
```

Layering rules:

- `PaycheckCalculator.Core` has no UI, HTTP, MAUI, database, or persistence dependency.
- `PaycheckCalculator.Shared` references Core and owns DTOs, JSON serialization, deterministic mergers, API client code, store abstractions, and entitlement abstractions.
- `PaycheckCalculator.Api` references Shared and persists sync state with EF Core/PostgreSQL.
- Front-ends reference Core and Shared, not each other.

---

## JSON Tax Data

Tax data lives in [`PaycheckCalculator.Core/Data/`](../../PaycheckCalculator.Core/Data/):

| File | Description |
|---|---|
| `us_irs_15t_2026_percentage_automated.json` | IRS Publication 15-T 2026 percentage-method data |
| `ar_withholding_2026.json` | Arkansas withholding tables |
| `ca_method_b_2026.json` | California Method B withholding data |
| `co_dr0004_2026.json` | Colorado DR 0004 Table 1 allowance data |
| `connecticut_withholding_2026.json` | Connecticut withholding tables |
| `ok_ow2_2026_percentage.json` | Oklahoma OW-2 withholding tables |
| `state_supplemental_2026.json` | State supplemental (bonus) withholding rates for all 50 states + DC |
| `tax_source_manifest_2026.json` | Canonical official-source metadata, implementation mapping, approximations, and exclusions |
| `Schemas/*.json` | Dynamic state-input schema files for every state / DC |

`AddPaycheckCalculatorCore` reads the JSON files at startup, validates the source catalog and state calculator mapping, and registers the corresponding calculators and schema provider. If a JSON file is renamed or moved, update all consumers: Core loader/output content, MAUI assets, Blazor `TaxData` links, test project links, and any tests or documentation that reference the old name.
