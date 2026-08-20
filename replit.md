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

This project pins `global.json` to the **.NET 10 SDK** (`10.0.400`). Replit's Nix modules may not
carry that exact patch, so the SDK is installed locally instead:

- Downloaded via `dotnet-install.sh` into `.dotnet/` (git-ignored) — matches the exact version pinned in `global.json`.
- `icu` was added as a system dependency (required by the SDK's globalization support).
- The two `start-*.sh` scripts prepend `.dotnet/` to `PATH` and set `DOTNET_ROOT` so `dotnet` resolves to the pinned SDK instead of any older system-wide install.

If `global.json` is ever bumped, re-run `dotnet-install.sh --version <new-version> --install-dir .dotnet` to refresh it.

### Database

`PaycheckCalculator.API` uses EF Core migrations against PostgreSQL. It's wired to Replit's built-in Postgres database via the standard `PGHOST`/`PGPORT`/`PGDATABASE`/`PGUSER`/`PGPASSWORD` environment variables (composed into `ConnectionStrings__Sync` inside `start-api.sh`, not hardcoded). Migrations apply automatically on startup (`db.Database.Migrate()` in `Program.cs`), which can be
turned off with `Database__MigrateOnStartup=false` when migrations run as a separate release step.
`ConnectionStrings__Sync` is **required** — the API refuses to start without it rather than falling
back to a default local database.

### Notes

- HTTPS redirection in `PaycheckCalculator.Blazor/Program.cs` is skipped only in Development — Replit terminates TLS at its edge proxy and forwards plain HTTP there, so an HTTPS redirect would break the proxied preview. It stays enabled in all other environments as a safety net.
- The MAUI app (`PaycheckCalculator.App`) is untouched and not part of the Replit run setup.

## User preferences

None recorded yet.
