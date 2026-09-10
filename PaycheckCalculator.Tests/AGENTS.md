# AGENTS.md - PaycheckCalculator.Tests

Scope: this file applies to everything under `PaycheckCalculator.Tests/`.

## Role of this project

`PaycheckCalculator.Tests` is the xUnit suite for Core calculation behavior, Shared serialization/merge/sync behavior, API integration behavior, and Blazor export paths. It intentionally does not reference the MAUI app so the suite can run on Linux/CI without the MAUI workload.

## Test design rules

- Use clear scenario-based test names that describe the rule being protected.
- Use explicit expected numeric values from source tables, legal rules, examples, or hand calculations. Do not compute expected values by calling production helpers that could contain the same bug.
- Prefer focused tests for one rule or edge case at a time. Add broader pipeline tests only when the interaction is the point of the test.
- Cover rounding to cents, whole-dollar state rules, bracket boundaries, exemptions, allowances, W-4 adjustments, YTD FICA caps, pre-tax deduction flags, paid-leave/disability premiums, gross-up convergence, annual projections, and self-employment estimates.

## Project reference rules

- Keep the suite free of MAUI references.
- The Blazor project reference is aliased to avoid `Program` type collisions. Preserve the alias unless replacing the collision strategy deliberately.
- Keep Core tax JSON and schema links in sync with Core/App/Blazor data names. If a JSON file is renamed, update the linked item here and all tests that load it.

## API and sync tests

- API tests should not require a local PostgreSQL instance. Use the existing test host/test configuration pattern.
- Test authentication, ownership boundaries, merge persistence, tombstones, and round-trip JSON for sync changes.
- Shared contract tests should verify backward-compatible serialization whenever DTOs or converters change.

## Useful commands

```bash
dotnet test PaycheckCalculator.Tests
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~Federal"
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~California|FullyQualifiedName~Oklahoma"
dotnet test PaycheckCalculator.Tests --filter "DisplayName~Rounds"
```

The full test project targets `net10.0` and should remain runnable without the MAUI workload.
