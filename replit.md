# PaycheckCalc

A US paycheck calculator for 2026 withholding rules (federal, FICA, state, disability/paid-leave premiums, deductions, annual projections, gross-up, and self-employment/1099 estimates for all 50 states + DC). See `README.md` for full feature details and architecture.

## Solution layout

- `PaycheckCalc.Core` — UI-agnostic tax/pay/budget calculation engines.
- `PaycheckCalc.Shared` — sync DTOs, JSON config, API client, entitlement abstractions.
- `PaycheckCalc.Blazor` — Blazor Server web front-end (runs in Replit).
- `PaycheckCalc.Api` — optional ASP.NET Core Web API for account sync (runs in Replit).
- `PaycheckCalc.App` — .NET MAUI app for Android/Windows (cannot run in Replit's preview; build/run it on your own machine or CI).
- `PaycheckCalc.Tests` — unit + integration tests.

## Running in Replit

Two workflows are configured and running:

- **Blazor Web** (`bash start-blazor.sh`) — the user-facing web app, port 5000 (webview).
- **Sync API** (`bash start-api.sh`) — the accounts/sync backend, port 5201 (console, internal only). It binds `127.0.0.1:5201` and has no external port mapping in `.replit`, so it's reachable only from the Blazor process over `localhost`, never from outside the Repl.

Both scripts export `ASPNETCORE_URLS`/`DOTNET_ROOT`/`PATH` and start the process; restart the workflows after code changes.

### .NET SDK

This project pins `global.json` to a **.NET 11 preview SDK** (`11.0.100-preview.5.26302.115`), which isn't available as a Replit Nix module (the highest module is .NET 10). It's installed locally instead:

- Downloaded via `dotnet-install.sh` into `.dotnet/` (git-ignored) — matches the exact preview version pinned in `global.json`.
- `icu` was added as a system dependency (required by the preview SDK's globalization support).
- The two `start-*.sh` scripts prepend `.dotnet/` to `PATH` and set `DOTNET_ROOT` so `dotnet` resolves to the preview SDK instead of the system-wide .NET 7.

If `global.json` is ever bumped to a newer preview build, re-run `dotnet-install.sh --version <new-version> --install-dir .dotnet` to refresh it.

### Database

`PaycheckCalc.Api` uses EF Core migrations against PostgreSQL. It's wired to Replit's built-in Postgres database via the standard `PGHOST`/`PGPORT`/`PGDATABASE`/`PGUSER`/`PGPASSWORD` environment variables (composed into `ConnectionStrings__Sync` inside `start-api.sh`, not hardcoded). Migrations apply automatically on startup (`db.Database.Migrate()` in `Program.cs`).

### Notes

- HTTPS redirection in `PaycheckCalc.Blazor/Program.cs` is skipped only in Development — Replit terminates TLS at its edge proxy and forwards plain HTTP there, so an HTTPS redirect would break the proxied preview. It stays enabled in all other environments as a safety net.
- The MAUI app (`PaycheckCalc.App`) is untouched and not part of the Replit run setup.

## User preferences

None recorded yet.
