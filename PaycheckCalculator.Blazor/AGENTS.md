# AGENTS.md - PaycheckCalculator.Blazor

Scope: this file applies to everything under `PaycheckCalculator.Blazor/`.

## Role of this project

`PaycheckCalculator.Blazor` is the Blazor Server front-end. It exposes the paycheck calculator, annual projection, saved paycheck comparison, budget features, account/session UI, exports, print support, and public state landing pages using Core and Shared services.

## Architecture rules

- Keep Razor components focused on rendering, binding, and simple UI orchestration. Do not put tax, gross-up, annual projection, budget, or sync merge logic directly in `.razor` files.
- Use Core for calculations and Shared for DTOs, JSON configuration, sync services, API client behavior, and entitlement abstractions.
- Keep dynamic state inputs schema-driven from Core schema data. Do not fork state-specific UI rules into Razor unless the schema cannot represent them and the exception is documented.
- Server-side calls to the sync API should stay server-side; do not introduce CORS-dependent browser API calls without an explicit requirement.
- Anonymous saved paycheck and budget state is session/circuit scoped unless the task explicitly changes persistence.

## Tax data and exports

- Core tax data is linked into build output under `TaxData/`; keep link names aligned with Core loader expectations, especially `TaxData/schemas/*.json`.
- CSV/PDF export logic belongs in `Services/Export/` and should render from domain results plus projection/comparison data. Do not recalculate values inside exporters.
- Keep `wwwroot/export.js` limited to browser download/print glue. Business logic belongs in C# services.
- `wwwroot/auto-select.js` handles select-all-on-focus for inputs globally; do not add per-field focus/select handlers in Razor markup.
- Maintain print styles in CSS and avoid layout changes that break printable results.

## UI consistency

- Keep the Blazor calculator behavior aligned with MAUI where practical: same input concepts, same result split between per-paycheck and annual, same Show Your Work semantics, same A/B comparison meaning.
- Prefer shared helpers/models over duplicating formatting logic in multiple components.

## Useful commands

```bash
dotnet build PaycheckCalculator.Blazor
dotnet run --project PaycheckCalculator.Blazor
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~Export"
```

This project targets `net10.0` and should build on Linux/CI without the MAUI workload.
