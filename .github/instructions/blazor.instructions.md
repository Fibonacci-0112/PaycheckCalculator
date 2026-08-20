---
applyTo: "PaycheckCalculator.Blazor/**/*.cs,PaycheckCalculator.Blazor/**/*.razor"
---

# Blazor project instructions

- `PaycheckCalculator.Blazor` is the Blazor Server front-end. Keep Razor components focused on rendering, binding, and simple UI orchestration. Do not put tax, gross-up, annual projection, bonus, budget, or sync merge logic in `.razor` files or code-behind.
- Use Core for all calculations. Use Shared for DTOs, JSON configuration, sync services, API client, and entitlement abstractions.

## State inputs and schema

- State input fields must remain schema-driven from `Core`'s `IStateWithholdingCalculator.GetInputSchema()`. Do not hardcode per-state UI rules in Razor components if the schema can express the requirement.
- Keep schema file links under `TaxData/schemas/*.json` in the build output aligned with Core loader expectations. If Core renames a schema file, update the Blazor `.csproj` linker entry too.

## Session scope and persistence

- Anonymous saved-paycheck state uses `SessionPaycheckStore`; anonymous budget state uses `SessionBudgetStore`. Both keep a circuit-scoped working set and mirror it to browser `localStorage` through `BrowserLocalStorage`, so data survives a refresh, a closed tab, and a new circuit.
- JS interop is unavailable during prerender, so the stores hydrate lazily: call `EnsureHydratedAsync()` from `OnAfterRenderAsync(firstRender)` and re-read the store there. Persistence is best-effort — a blocked or full `localStorage` must never break the page.
- Server-side calls to the sync API (`PaycheckApiClient`) must remain server-side. Do not introduce CORS-dependent browser fetch calls unless an explicit requirement forces it.

## Exports and print

- CSV and PDF export logic belongs in `Services/Export/`. Renderers accept domain results plus optional `AnnualProjection`/`ComparisonRow`s and return bytes; they must not recalculate values.
- `wwwroot/export.js` is browser download/print glue only. Business logic belongs in C# services.
- Maintain `@media print` styles in `app.css` and avoid layout changes that break printable results.

## UI consistency with MAUI

- Keep the Blazor calculator behavior aligned with the MAUI app where practical: same input concepts, same per-paycheck / annual result split, same Show Your Work semantics, same A/B comparison meaning.
- Prefer shared helpers and models over duplicating formatting logic across multiple components.

## Tax data wiring

- Core tax JSON is linked into build output under `TaxData/` via `PaycheckCalculator.Blazor.csproj`. Do not rename or reorganize these links without also updating Core, the `FileSystemTaxDataReader`, and every `.csproj` entry that depends on stable names.
