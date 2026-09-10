---
name: .NET preview SDK not available as a Replit module
description: How to get a project running when global.json pins a .NET SDK version newer than any Replit dotnet-* module (e.g. .NET 11 preview when only dotnet-10.0 exists).
---

> **No longer applies to this repository.** `global.json` now pins the .NET 10 GA SDK
> (`10.0.400`), which Replit's `dotnet-10.0` module provides directly. The technique below is
> kept because it applies to any project pinning an SDK newer than the available modules; the
> .NET 11 preview references in it are the original worked example, not current state.

Replit's `listAvailableModules({ language: "dotnet" })` currently tops out at `dotnet-10.0`. A project whose `global.json` pins a preview SDK (e.g. `11.0.100-preview.x`) with hard-coded `net11.0` TargetFrameworks across its non-test-only projects cannot be downgraded without editing every csproj — instead, install the exact pinned SDK manually via Microsoft's `dotnet-install.sh` script into a project-local directory (e.g. `.dotnet/`, git-ignored), then export `PATH`/`DOTNET_ROOT` to point at it before running `dotnet` commands or workflow scripts.

**Why:** the SDK is otherwise unobtainable through Replit's module system, and editing global.json / retargeting csproj files would be a much bigger, riskier change than the user asked for.

**How to apply:** `curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh && ./dotnet-install.sh --version <exact-version-from-global.json> --install-dir .dotnet`. The preview SDK also needs `icu` installed as a system dependency (`installSystemDependencies({ packages: ["icu"] })`) or `dotnet` aborts with a missing-ICU globalization error. Any workflow/run script must `export PATH="$PWD/.dotnet:$PATH"` and `export DOTNET_ROOT="$PWD/.dotnet"` first, since the system-wide `dotnet` (Nix module) will otherwise shadow it.
