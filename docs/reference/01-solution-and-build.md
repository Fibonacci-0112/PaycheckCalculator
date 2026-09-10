# 01 — Solution, Projects & Build

How the repository is laid out, what each project is allowed to depend on, how it is built, and
how it is run.

---

## The solution file

The solution uses the newer XML **`.slnx`** format rather than the classic `.sln`:

```xml
<!-- PaycheckCalculator.slnx -->
<Solution>
  <Project Path="PaycheckCalculator.API/PaycheckCalculator.API.csproj" />
  <Project Path="PaycheckCalculator.App/PaycheckCalculator.App.csproj">
    <Deploy Solution="Debug|*" />
  </Project>
  <Project Path="PaycheckCalculator.Blazor/PaycheckCalculator.Blazor.csproj" />
  <Project Path="PaycheckCalculator.Core/PaycheckCalculator.Core.csproj" />
  <Project Path="PaycheckCalculator.Shared/PaycheckCalculator.Shared.csproj" />
  <Project Path="PaycheckCalculator.Tests/PaycheckCalculator.Tests.csproj" />
</Solution>
```

The `<Deploy Solution="Debug|*" />` entry on the MAUI app marks it for deployment in Debug
configurations, the analogue of the classic `.sln` deploy flag.

---

## The six projects

| Project | SDK | Target framework(s) | Role |
|---|---|---|---|
| `PaycheckCalculator.Core` | `Microsoft.NET.Sdk` | `net10.0` | All tax/pay/budget math. No UI, HTTP, or persistence. |
| `PaycheckCalculator.Shared` | `Microsoft.NET.Sdk` | `net10.0` | Sync DTOs, JSON config, mergers, typed HTTP client, store + entitlement abstractions. |
| `PaycheckCalculator.API` | `Microsoft.NET.Sdk.Web` | `net10.0` | ASP.NET Core minimal API: Identity accounts + authorized sync. |
| `PaycheckCalculator.Blazor` | `Microsoft.NET.Sdk.Web` | `net10.0` | Blazor Server web front-end. |
| `PaycheckCalculator.App` | `Microsoft.NET.Sdk` (`UseMaui`) | `net10.0-android`, `-ios`, `-maccatalyst`, `-windows10.0.19041.0` | .NET MAUI native front-end. |
| `PaycheckCalculator.Tests` | `Microsoft.NET.Sdk` | `net10.0` | xUnit suite covering Core, Shared, Api, and the Blazor exporters. |

Every project sets `ImplicitUsings=enable` and `Nullable=enable`.

### MAUI target-framework conditioning

`PaycheckCalculator.App.csproj` only adds platform targets the *build host* can actually
produce:

```xml
<TargetFrameworks>net10.0-android</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx'))">$(TargetFrameworks);net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
```

So Android builds everywhere, Apple targets only on macOS, and Windows only on Windows. Minimum
platform versions: iOS/Mac Catalyst 15.0, Android API 24.

Two platform-specific workarounds live in the same file and are worth knowing before you
"clean them up":

- **Android Debug sets `EmbedAssembliesIntoApk=true`.** MAUI's Fast Deployment relies on
  Android's `run-as`, which some devices and emulators only partially support, producing
  `XA0129` deploy errors. Embedding assemblies sidesteps Fast Deployment entirely; debugging
  still works, deployment is just slower.
- **Windows sets `WindowsPackageType=None` and suppresses `WMC1006`.** The Windows XAML
  compiler emits design-time warnings for project-reference outputs before Core/Shared have
  been built.

---

## Dependency graph and layering rules

```text
PaycheckCalculator.Core          (depends on nothing in-repo)
        ▲
        │
PaycheckCalculator.Shared        → Core
        ▲
        │
PaycheckCalculator.API           → Shared

PaycheckCalculator.App    (MAUI)  → Core + Shared
PaycheckCalculator.Blazor         → Core + Shared
PaycheckCalculator.Tests          → Core + Shared + Api + Blazor
```

The rules that make this graph meaningful — treat them as invariants, not preferences:

1. **Core references no UI, HTTP, EF Core, or file-system persistence.** Its only NuGet
   dependency is `Microsoft.Extensions.Logging.Debug`. Tax data reaches it through the
   `ITaxDataReader` abstraction that the *host* implements.
2. **Shared references Core only.** It is the portable wire/storage layer, safe to link into
   any client.
3. **Api references Shared and must never reference App or Blazor.**
4. **The front-ends never reference Api.** They talk to it over HTTP through
   `PaycheckApiClient`, which lives in Shared.

`PaycheckCalculator.Tests` deliberately reaches across all of them so it can test the API
end-to-end and the Blazor export renderers without a browser.

### Two notable cross-project mechanics

`PaycheckCalculator.Blazor.csproj` exposes internals to the test project so component helper
types can be unit-tested without a bUnit render harness:

```xml
<InternalsVisibleTo Include="PaycheckCalculator.Tests" />
```

That is what makes `Calculator.StateFieldVm` reachable from `StateFieldVmTest`.

`PaycheckCalculator.Tests.csproj` references Blazor under an **extern alias** because both web
hosts generate a top-level `Program` type:

```xml
<ProjectReference Include="..\PaycheckCalculator.Blazor\PaycheckCalculator.Blazor.csproj">
  <Aliases>blazor</Aliases>
</ProjectReference>
```

Without the alias, the Blazor host's `Program` would collide with
`PaycheckCalculator.API`'s `Program` — which `SyncApiTest` needs in the global namespace for
`WebApplicationFactory<Program>`.

---

## SDK pinning

```json
// global.json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature"
  }
}
```

The repo runs on the **.NET 10 GA SDK**. `latestFeature` roll-forward lets a newer patch of the
same feature band satisfy the requirement. Package versions across the graph are the matching
10.0 releases (`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11,
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.Maui.Controls` 10.0.90); no preview
packages remain.

The MAUI project additionally sets `<LangVersion>preview</LangVersion>`, which is required for
the `[ObservableProperty]`-on-partial-property syntax used throughout the view models (see
[13 — The MAUI App](13-maui-app.md)).

Claude Code on the web installs this SDK automatically via the `SessionStart` hook
`.claude/hooks/session-start.sh`, which reads the pin out of `global.json` at run time.
[`.agents/memory/dotnet-preview-sdk-setup.md`](../../.agents/memory/dotnet-preview-sdk-setup.md)
records the older manual workaround from when the repo pinned a preview SDK; it no longer
applies here.

---

## NuGet dependencies

| Project | Package | Purpose |
|---|---|---|
| Core | `Microsoft.Extensions.Logging.Debug` | Only external dependency in the engine. |
| Api | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity user store. |
| Api | `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL provider. |
| Api | `Microsoft.EntityFrameworkCore.Design` | `dotnet ef` tooling; `PrivateAssets=all` keeps it out of the referencing test project. |
| App | `CommunityToolkit.Mvvm` 8.4.2 | MVVM source generators. |
| App | `Microsoft.Maui.Controls` | MAUI UI. |
| Tests | `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Test framework and runner. |
| Tests | `Microsoft.EntityFrameworkCore.Sqlite` | In-memory-ish DB for API integration tests. |
| Tests | `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory` host for the API. |

Blazor and Shared pull in no NuGet packages of their own — only project references and the
shared framework.

---

## Building, testing, running

All commands run from the repository root.

```bash
# Whole solution (requires the MAUI workload)
dotnet build

# A single project — Core, Shared, Api, Blazor, and Tests all build without MAUI
dotnet build PaycheckCalculator.Core

# All tests (transitively builds Core, Shared, Api, and Blazor)
dotnet test PaycheckCalculator.Tests

# One test class, or one test
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"

# Blazor web app (no MAUI workload needed)
dotnet run --project PaycheckCalculator.Blazor

# Sync API (no MAUI workload needed; http profile defaults to http://localhost:5201)
dotnet run --project PaycheckCalculator.API

# MAUI app (requires `dotnet workload install maui` and a target platform)
dotnet build PaycheckCalculator.App
dotnet run --project PaycheckCalculator.App
```

**Linux/CI reality check:** `Core`, `Shared`, `Api`, `Blazor`, and `Tests` all target plain
`net10.0` and build on Linux with no MAUI workload installed. Only `PaycheckCalculator.App`
needs the workload. That is why CI can validate the entire engine and both server-side
projects without ever touching MAUI.

For end-to-end account/sync testing, run the API and the Blazor app together — the Blazor app
calls the API **server-side from the circuit**, so there is no CORS configuration to worry
about.

---

## Running a local PostgreSQL

The API defaults to
`Host=localhost;Port=5432;Database=paycheckcalculator_dev;Username=admin;Password=password` when
no `ConnectionStrings:Sync` is configured — the same credentials
[`compose.yml`](../../compose.yml) seeds, so the two line up with no configuration:

```bash
docker compose up -d postgres
```

It runs `postgres:18`, binds to `127.0.0.1:5432` only, seeds the `paycheckcalculator_dev` database
for user `admin`, includes a `pg_isready` health check, and persists data in the `pgdata` volume.
The loopback-only binding is deliberate: the seeded credentials are public, so the database must not
be reachable from the local network. Note that Docker's published ports bypass `ufw`/`firewalld` on
Linux, so a host firewall would not otherwise contain it.

`appsettings.Development.json` carries the same connection string under `ConnectionStrings:Sync`,
so a `dotnet run` in Development uses it without any environment variables. If the database is
unreachable, startup fails with a single `crit:` line naming the host, port, database, and user it
tried, and the process exits with code 1.

---

## Container / Replit run scripts

Two scripts pin a repo-local SDK and wire the two web processes together:

**[`start-api.sh`](../../start-api.sh)** — puts `./.dotnet` on `PATH`, sets
`ASPNETCORE_URLS=http://127.0.0.1:5201`, and builds `ConnectionStrings__Sync` from the standard
`PG*` environment variables with `SSL Mode=Disable`.

**[`start-blazor.sh`](../../start-blazor.sh)** — binds `http://0.0.0.0:5000` and points
`PaycheckApi__BaseUrl` at `http://127.0.0.1:5201` so the web app finds the API.

Note the double-underscore form (`ConnectionStrings__Sync`, `PaycheckApi__BaseUrl`): that is
the ASP.NET Core environment-variable convention for nested configuration keys.

One Blazor startup detail exists specifically for this environment — HTTPS redirection is
skipped in Development because Replit terminates TLS at its edge proxy and forwards plain HTTP:

```csharp
// PaycheckCalculator.Blazor/Program.cs
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
```

See also [`replit.md`](../../replit.md) and [`replit.nix`](../../replit.nix).

---

## Continuous integration

### `.github/workflows/dotnet.yml`

Runs on push and pull request against `main`, on `ubuntu-latest`:

Two parallel jobs.

`build`:

1. `actions/setup-dotnet@v4` with the pinned SDK `10.0.400`
2. `dotnet restore PaycheckCalculator.Tests`
3. `dotnet build PaycheckCalculator.Tests --no-restore`
4. `dotnet test PaycheckCalculator.Tests --no-build --verbosity normal`

Targeting the test project alone transitively restores, builds, and validates Core, Shared,
Api, and Blazor through project references — while never requiring the MAUI workload.

`MAUI Android build`:

1. `actions/setup-dotnet@v4` reading the pin from `global.json`
2. `dotnet workload install maui-android`
3. `dotnet build … -t:InstallAndroidDependencies` to provision the Android SDK platform, which
   the runner's preinstalled SDK does not necessarily carry
4. `dotnet restore` + `dotnet build PaycheckCalculator.App -f net10.0-android`

Android is the only MAUI target that builds on a Linux runner, so iOS, Mac Catalyst, and
Windows remain uncovered by CI. `permissions: contents: read` keeps the token minimal.

### `.github/workflows/codeql.yml`

CodeQL advanced setup, on push, pull request, a weekly Sunday 03:00 UTC schedule, and
`workflow_dispatch`. It skips PRs from forks (`github.event.pull_request.head.repo.fork == false`)
because their read-only token cannot upload results. Permissions are denied at the workflow level
and granted per job (`security-events: write`, `packages: read`, `actions: read`, `contents: read`).

A matrix analyzes one language per job, each with its own build mode:

| Language | Build mode | Covers |
| --- | --- | --- |
| `csharp` | `manual` | Core, Shared, Api, Blazor, Tests |
| `javascript-typescript` | `none` | `PaycheckCalculator.Blazor/wwwroot/export.js` |
| `actions` | `none` | `.github/workflows/*` |

`autobuild` cannot be used: it walks the whole solution and fails on `PaycheckCalculator.App`
with `NETSDK1147` because the MAUI workloads are not installed on GitHub's Linux runners. The
manual build instead runs `actions/setup-dotnet` against `global.json`, then restores and builds
`PaycheckCalculator.Tests` — the same Linux-buildable set as `dotnet.yml`, including the generated
Razor code. Each job uploads under `category: "/language:<language>"`.

**CodeQL does not build MAUI**, so `PaycheckCalculator.App` is outside CodeQL's C# coverage even
though `dotnet.yml`'s `MAUI Android build` job now compiles it. Android compilation breakage is
caught by CI; a change that only breaks the iOS, Mac Catalyst, or Windows targets is not, so build
those locally when you touch platform-specific code.

---

## Repository layout

```text
PaycheckCalculator/
├── PaycheckCalculator.slnx          # Solution (XML .slnx format)
├── global.json                      # Pinned .NET 10 SDK
├── compose.yml                      # Local PostgreSQL
├── start-api.sh / start-blazor.sh   # Container run scripts
├── CLAUDE.md / AGENTS.md            # Agent guidance (kept in sync with each other)
├── README.md / ROADMAP.md
├── .github/
│   ├── workflows/                   # dotnet.yml, codeql.yml
│   ├── instructions/                # Per-area Copilot instruction files
│   ├── copilot-instructions.md
│   └── ISSUE_TEMPLATE/accuracy-incident.yml
├── .agents/memory/                  # Agent working notes
├── docs/
│   ├── reference/                   # ← this set
│   ├── wiki/                        # Task-oriented guides
│   ├── class-diagram.md
│   ├── brand/                       # App-icon source art + notes
│   └── US_State_Withholding_Tax_Sources_2026.xlsx
├── PaycheckCalculator.Core/
│   ├── Models/          Pay/        Tax/        Budgeting/
│   ├── Explanation/     Validation/ DependencyInjection/
│   └── Data/            # Tax JSON + Schemas/*.json
├── PaycheckCalculator.Shared/
│   ├── Snapshots/  Sync/  Budgeting/  Client/  Json/  Entitlements/
├── PaycheckCalculator.API/
│   ├── Data/  Endpoints/  Migrations/
├── PaycheckCalculator.Blazor/
│   ├── Components/  Services/  Models/  wwwroot/
├── PaycheckCalculator.App/
│   ├── Views/  ViewModels/  Models/  Mappers/  Helpers/
│   ├── Controls/  Behaviors/  Services/  Platforms/  Resources/
└── PaycheckCalculator.Tests/        # 83 test files, flat
```

Each project also carries its own `AGENTS.md` with area-specific rules — see
[15 — Conventions & Workflows](15-conventions-and-workflows.md).

---

**Next:** [02 — Core Domain Model](02-core-domain-model.md)
