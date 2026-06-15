# Getting Started

This page covers everything you need to build, test, and run PaycheckCalc.

---

## Prerequisites

- **[.NET 11 SDK](https://dotnet.microsoft.com/)** (preview) — the SDK version is pinned in
  [`global.json`](../../global.json) (`11.0.100-preview.5.26302.115`, `latestPatch` roll-forward,
  prerelease allowed).
- **.NET MAUI workload** (required only for the MAUI App project):
  ```bash
  dotnet workload install maui
  ```
- **Android SDK** or **Windows 10+ SDK** — depending on your MAUI target platform.
- **PostgreSQL** — only required to *run* the sync API (`PaycheckCalc.Api`). The integration tests use an
  in-memory SQLite database instead, so you don't need PostgreSQL just to build and test.

`PaycheckCalc.Core`, `PaycheckCalc.Shared`, `PaycheckCalc.Api`, `PaycheckCalc.Blazor`, and
`PaycheckCalc.Tests` build **without** the MAUI workload on any OS supported by the .NET 11 SDK. Only
`PaycheckCalc.App` (MAUI) needs the workload. `PaycheckCalc.Core` multi-targets `net11.0;net9.0` when the
.NET 11 SDK is present (otherwise `net9.0` only); the other non-MAUI projects are `net11.0`.

---

## Repository Layout

```
PaycheckCalc.slnx                  ← Solution file
├── PaycheckCalc.Core/             ← Business logic (no UI dependencies); tax + budget engines
├── PaycheckCalc.App/              ← .NET MAUI frontend (Android & Windows)
├── PaycheckCalc.Blazor/           ← Blazor Server web frontend
├── PaycheckCalc.Shared/           ← Sync contracts, JSON, merge, HTTP client, store abstractions
├── PaycheckCalc.Api/              ← ASP.NET Core Web API: Identity accounts + sync (PostgreSQL/EF Core)
├── PaycheckCalc.Tests/            ← xUnit test suite (Core + Shared + Api + Blazor)
└── docs/                          ← Documentation and class diagrams
```

---

## Build

### Build a single non-MAUI project (no MAUI workload required)

```bash
dotnet build PaycheckCalc.Core
dotnet build PaycheckCalc.Blazor
dotnet build PaycheckCalc.Api
```

### Build the full solution (requires the MAUI workload)

```bash
dotnet build PaycheckCalc.slnx
```

---

## Run Tests

```bash
dotnet test PaycheckCalc.Tests
```

The suite has **1,200+** xUnit tests (currently ~1,248 `[Fact]`/`[Theory]` methods across ~70 files)
covering federal tax, FICA, every state calculator, gross-up, annual projection, budgeting, account/sync
merge, JSON round-trips, and the CSV/PDF exporters. CI (`.github/workflows/dotnet.yml`) restores, builds,
and tests `PaycheckCalc.Tests` on Linux (CodeQL runs separately in `codeql.yml`).

---

## Run the Apps

### Blazor web app (no MAUI workload needed)

```bash
dotnet run --project PaycheckCalc.Blazor
```

### Sync API (no MAUI workload needed)

```bash
dotnet run --project PaycheckCalc.Api      # defaults to http://localhost:5201; needs PostgreSQL
```

Configure the database with `ConnectionStrings:Sync` (default
`Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres`). EF Core migrations
are applied automatically at startup. Run the API and Blazor together for end-to-end account/sync testing.

### MAUI app (Android / Windows)

```bash
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

> **Note:** The MAUI app requires the `maui` workload and a supported target platform.

```bash
# Android
dotnet build PaycheckCalc.App -t:Run -f net11.0-android
# Windows
dotnet build PaycheckCalc.App -t:Run -f net11.0-windows10.0.19041.0
```

---

## Project Dependencies

```
PaycheckCalc.Shared  →  PaycheckCalc.Core
PaycheckCalc.App     →  PaycheckCalc.Core, PaycheckCalc.Shared
PaycheckCalc.Blazor  →  PaycheckCalc.Core, PaycheckCalc.Shared
PaycheckCalc.Api     →  PaycheckCalc.Shared
PaycheckCalc.Tests   →  PaycheckCalc.Core, PaycheckCalc.Shared, PaycheckCalc.Api, PaycheckCalc.Blazor
```

- **PaycheckCalc.Core** has no dependency on any UI project, MAUI, HTTP, or persistence. It can be built
  and tested independently and multi-targets `net11.0;net9.0`.
- **PaycheckCalc.Shared** references only Core; it owns the sync wire/storage contracts and the merge logic
  reused by the API and both clients.
- **PaycheckCalc.Api** references Shared (never the front-ends); Core stays HTTP- and persistence-free.
- **PaycheckCalc.Tests** references Core, Shared, Api, and Blazor (the last via an `blazor` alias, so it can
  exercise the Blazor export renderers).

---

## JSON Tax Data

Tax tables are stored as JSON files in [`PaycheckCalc.Core/Data/`](../../PaycheckCalc.Core/Data/):

| File | Description |
|---|---|
| `us_irs_15t_2026_percentage_automated.json` | IRS Publication 15-T 2026 percentage method brackets |
| `ok_ow2_2026_percentage.json` | Oklahoma OW-2 withholding tables |
| `ca_method_b_2026.json` | California Method B (EDD DE 44) brackets |
| `ca_2026_method_b_calculator_ready.json` | California pre-processed calculator data |
| `ar_withholding_2026.json` | Arkansas DFA formula method tables |
| `co_dr0004_2026.json` | Colorado DR 0004 Table 1 allowance data |
| `connecticut_withholding_2026.json` | Connecticut TPG-211 withholding tables |
| `Schemas/*.json` | One file per state (51 files) declaring its dynamic input schema |

`AddPaycheckCalcCore` reads six of these at startup (`us_irs_15t…`, `ar_…`, `ok_…`, `ca_method_b…`,
`co_…`, `connecticut_…`) plus every `Schemas/*.json`, and caches them for the process lifetime. The files
are content-linked into the consumers via each `.csproj`: `MauiAsset` items in `PaycheckCalc.App`, linked
into the `TaxData/` build output in `PaycheckCalc.Blazor`, and linked `None`/copy items in
`PaycheckCalc.Tests`. **If you rename a JSON file, update every linker entry and the loader.**
