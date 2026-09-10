# PaycheckCalculator

A US paycheck calculator for 2026 withholding rules (federal, FICA, state, disability/paid-leave premiums, deductions, annual projections, gross-up, and self-employment/1099 estimates for all 50 states + DC). See `README.md` for full feature details and architecture.

## Solution layout

- `PaycheckCalculator.Core` — UI-agnostic tax/pay/budget calculation engines.
- `PaycheckCalculator.Shared` — sync DTOs, JSON config, API client, entitlement abstractions.
- `PaycheckCalculator.Blazor` — Blazor Server web front-end (runs in Replit).
- `PaycheckCalculator.API` — optional ASP.NET Core Web API for account sync (runs in Replit).
- `PaycheckCalculator.App` — .NET MAUI app for Android, iOS, macOS (Mac Catalyst), and Windows (cannot run in Replit's preview; build/run it on a compatible machine or CI).
- `PaycheckCalculator.Tests` — unit + integration tests.

## Running in Replit

Two workflows are configured and running:

- **Blazor Web** (`bash start-blazor.sh`) — the user-facing web app, port 5000 (webview).
- **Sync API** (`bash start-api.sh`) — the accounts/sync backend, port 5201 (console, internal only). It binds `127.0.0.1:5201` and has no external port mapping in `.replit`, so it's reachable only from the Blazor process over `localhost`, never from outside the Repl.

Both scripts export `ASPNETCORE_URLS`/`DOTNET_ROOT`/`PATH` and start the process; restart the workflows after code changes.

### .NET SDK

`global.json` pins the **.NET 10 GA SDK** (`10.0.400`, `latestFeature` roll-forward). This is a
released SDK, so the earlier workaround — hand-installing a .NET 11 preview into `.dotnet/`
because no Nix module provided it — is no longer needed.

**This Replit setup has not been revalidated since that change and is known to be inconsistent:**

- `.replit` still declares the `dotnet-7.0` module, which cannot satisfy a `10.0.400` pin. A
  `dotnet-10.0` module is available and is what this project now needs.
- `start-api.sh` and `start-blazor.sh` still prepend `$PWD/.dotnet` to `PATH` and set
  `DOTNET_ROOT` to it, so they depend on a local SDK directory that is git-ignored and no longer
  provisioned by anything in the repo.

Repairing or replacing this is tracked as Milestone 3.1 in [`ROADMAP.md`](ROADMAP.md). Until then,
treat the commands below as describing intent rather than a working configuration.

### Database

`PaycheckCalculator.API` uses EF Core migrations against PostgreSQL. It's wired to Replit's built-in Postgres database via the standard `PGHOST`/`PGPORT`/`PGDATABASE`/`PGUSER`/`PGPASSWORD` environment variables (composed into `ConnectionStrings__Sync` inside `start-api.sh`, not hardcoded). Migrations apply automatically on startup (`db.Database.Migrate()` in `Program.cs`).

### Notes

- HTTPS redirection in `PaycheckCalculator.Blazor/Program.cs` is skipped only in Development — Replit terminates TLS at its edge proxy and forwards plain HTTP there, so an HTTPS redirect would break the proxied preview. It stays enabled in all other environments as a safety net.
- The MAUI app (`PaycheckCalculator.App`) is untouched and not part of the Replit run setup.

## User preferences

None recorded yet.
