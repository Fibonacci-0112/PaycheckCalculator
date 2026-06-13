# Getting Started

This page covers everything you need to build, test, and run PaycheckCalc.

---

## Prerequisites

- **[.NET 11 SDK](https://dotnet.microsoft.com/)** (preview) — the SDK version is pinned in [`global.json`](../../global.json).
- **.NET MAUI workload** (required only for the MAUI App project):
  ```bash
  dotnet workload install maui
  ```
- **Android SDK** or **Windows 10+ SDK** — depending on your MAUI target platform.

`PaycheckCalc.Core` and `PaycheckCalc.Tests` do **not** require the MAUI workload and can be built and tested on any OS supported by the .NET 11 SDK.

---

## Repository Layout

```
PaycheckCalc.slnx                  ← Solution file
├── PaycheckCalc.App/              ← .NET MAUI frontend (Android & Windows)
├── PaycheckCalc.Core/             ← Business logic (no UI dependencies)
├── PaycheckCalc.Tests/            ← xUnit test suite
└── docs/                          ← Documentation and class diagrams
```

---

## Build

### Build the Core Library (no MAUI workload required)

```bash
dotnet build PaycheckCalc.Core
```

### Build the Full Solution (requires MAUI workload)

```bash
dotnet build PaycheckCalc.slnx
```

---

## Run Tests

```bash
dotnet test PaycheckCalc.Tests
```

The test suite includes over 1,000 xUnit tests covering federal tax, FICA, all state calculators, and projection calculations.

---

## Run the App

### MAUI (Android / Windows)

```bash
dotnet build PaycheckCalc.App
dotnet run --project PaycheckCalc.App
```

> **Note:** The MAUI app requires the `maui` workload and a supported target platform (Android emulator/device or Windows).

#### Android

You can deploy to a connected device or emulator:

```bash
dotnet build PaycheckCalc.App -t:Run -f net11.0-android
```

#### Windows

```bash
dotnet build PaycheckCalc.App -t:Run -f net11.0-windows10.0.19041.0
```

---

## Project Dependencies

The dependency graph is intentionally simple:

```
PaycheckCalc.App     →  PaycheckCalc.Core
PaycheckCalc.Tests   →  PaycheckCalc.Core
```

- **PaycheckCalc.Core** has no dependency on any UI project or MAUI. It can be built and tested independently.
- **PaycheckCalc.App** references Core for all calculation logic.
- **PaycheckCalc.Tests** references Core to test business logic directly.

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
| `Schemas/*.json` | One file per state declaring its dynamic input schema |

These files are loaded once at startup via dependency injection (from `FileSystem.OpenAppPackageFileAsync` on MAUI, and from the build output directory in tests) and cached for the lifetime of the process.
