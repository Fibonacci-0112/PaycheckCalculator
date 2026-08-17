# 12 — The Blazor Web App

`PaycheckCalculator.Blazor` is an interactive Blazor Server application: one calculator page with
inputs and results side by side, a budget page, SEO-optimized per-state landing pages, and a
shared app shell — all rendered server-side over a persistent SignalR circuit.

---

## Render mode and hosting

```csharp
// Program.cs
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
...
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

```razor
{{-- Components/App.razor --}}
<HeadOutlet @rendermode="InteractiveServer" />
...
<Routes @rendermode="InteractiveServer" />
```

**Interactive Server** is set once, globally, on the root `<Routes>` component in `App.razor` —
individual pages (`Calculator.razor` notes this explicitly in a comment) must not redeclare
`@rendermode`, since a descendant re-declaring the mode its ancestor already set is a conflict.

Under Interactive Server, every component update, event handler invocation, and re-render happens
over a **circuit** — a persistent SignalR connection between the browser and this server process.
That single fact explains several other design choices in this project:

- **Scoped DI services are circuit-scoped, not request-scoped.** A service registered
  `AddScoped` lives for as long as the browser tab's circuit is open, not for a single HTTP
  request — which is exactly what makes `SessionPaycheckStore` and `CircuitAccountSession`
  (both `AddScoped`) behave as "lives until the tab closes" stores without any custom lifetime
  management.
- **State genuinely disappears when the tab closes.** The circuit closing is what triggers the
  scoped services' disposal — there's no polling, no explicit cleanup hook needed for the
  "anonymous data survives only until the browser tab closes" requirement; it falls out of the
  render-mode choice for free.

---

## Service registration

```csharp
// Program.cs
var taxDataPath = Path.Combine(AppContext.BaseDirectory, "TaxData");
builder.Services.AddPaycheckCalculatorCore(new FileSystemTaxDataReader(taxDataPath));

builder.Services.AddScoped<SessionBudgetStore>();
builder.Services.AddScoped<IBudgetStore>(sp => sp.GetRequiredService<SessionBudgetStore>());

builder.Services.AddScoped<IEntitlementProvider, FreeEntitlementProvider>();

builder.Services.AddScoped<CircuitAccountSession>();
builder.Services.AddScoped<ITokenStore>(sp => sp.GetRequiredService<CircuitAccountSession>());
builder.Services.AddSingleton<IApiBaseAddressProvider, ConfigApiBaseAddressProvider>();
builder.Services.AddScoped<SessionPaycheckStore>();
builder.Services.AddScoped<ISavedPaycheckStore>(sp => sp.GetRequiredService<SessionPaycheckStore>());
builder.Services.AddHttpClient<PaycheckApiClient>();
builder.Services.AddScoped<PaycheckSyncService>();
```

The double-registration pattern (`AddScoped<SessionPaycheckStore>()` then
`AddScoped<ISavedPaycheckStore>(sp => sp.GetRequiredService<SessionPaycheckStore>())`) is
deliberate: it lets `CircuitAccountSession`-typed and `SessionPaycheckStore`-typed injection sites
(where the concrete type exposes extra members the interface doesn't, like
`CircuitAccountSession.Email` or `.IsSignedIn`) resolve to the **same instance** as
interface-typed injection sites, rather than two separate objects. `PaycheckApiClient` is the only
piece registered with a *typed* `HttpClient` (`AddHttpClient<PaycheckApiClient>()`), so its
`HttpClient` gets the standard `IHttpClientFactory` handler pooling.

`AddPaycheckCalculatorCore` is called exactly once here — every calculator it wires up
(`PayCalculator`, `AnnualProjectionCalculator`, `GrossUpCalculator`, `BonusCalculator`,
`SelfEmploymentCalculator`, `BudgetCalculator`, etc.) becomes a **singleton**, safely shared
across every concurrent circuit, because the calculators themselves are stateless.

---

## Reading tax data: `FileSystemTaxDataReader`

```csharp
public sealed class FileSystemTaxDataReader : ITaxDataReader
{
    public string ReadAllText(string logicalName)
    {
        var path = Path.Combine(_basePath, logicalName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) throw new FileNotFoundException($"Tax data file not found: {logicalName}", path);
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
```

`_basePath` is `AppContext.BaseDirectory/TaxData`, matching the `<Link>TaxData\...</Link>` entries
in `PaycheckCalculator.Blazor.csproj` (see
[09 — Tax Data Files](09-tax-data-files.md#2-paycheckcalculatorblazorcsproj--linked-into-taxdata)).
The `/` → platform-separator translation is what lets `AddPaycheckCalculatorCore` request
`"schemas/ca.json"` with a forward slash regardless of host OS.

---

## App shell: layout, routing, chrome

`Components/App.razor` is the HTML document shell — `app.css`, the generated
`PaycheckCalculator.Blazor.styles.css` (component-scoped CSS bundle), `blazor.web.js`, and the
project's own `export.js`.

`Components/Routes.razor` is a bare `<Router>` wrapping every page in `Layout.MainLayout` with
`FocusOnNavigate` targeting `h1` for accessibility on navigation.

### `MainLayout` + `ShellState` — page-owned chrome

`MainLayout.razor` renders a fixed sidebar (brand mark, nav links to Calculator/Budget/Saved
Paychecks/Account, privacy/terms footer) and a top bar (page title, a status pill showing
`"2026 IRS tables current"`, and an optional right-aligned action slot) around `@Body`.

The layout does not know anything about the calculator or the budget page. Instead, a small
cascading state object lets **pages push chrome content up to the layout that hosts them**:

```csharp
public sealed class ShellState
{
    public string Title { get; private set; } = "Paycheck Calculator";
    public RenderFragment? Actions { get; private set; }
    public int SavedCount { get; private set; }
    public event Action? Changed;

    public void Set(string title, RenderFragment? actions = null) { Title = title; Actions = actions; Changed?.Invoke(); }
    public void SetSavedCount(int count) { if (SavedCount == count) return; SavedCount = count; Changed?.Invoke(); }
    public void Refresh() => Changed?.Invoke();
}
```

`MainLayout` creates one `ShellState` instance and cascades it (`<CascadingValue Value="_shell" IsFixed="true">`).
`Calculator.razor` injects it and calls `Shell.Set(PageTitle ?? "Paycheck Calculator", TopBarActions)`
from its lifecycle, pushing a page-specific title and an export-button `RenderFragment` into the
shared top bar. The layout re-renders on `Changed`, marshaled onto the render sync context:

```csharp
private void OnShellChanged() => InvokeAsync(StateHasChanged);
```

The `InvokeAsync` wrapper matters: a page can push chrome state from its own `OnInitialized`
*while the layout itself is still rendering*, and `InvokeAsync` queues the re-render onto the
renderer's sync context instead of throwing a reentrancy exception.

---

## Pages

| Route | Component | Purpose |
|---|---|---|
| `/` | `Home.razor` | SEO head tags + `<Calculator />` with no pre-selected state |
| `/{StateSlug}-paycheck-calculator` | `StateLandingPage.razor` | Per-state SEO landing page, pre-selects that state |
| `/budget` | `Budget.razor` | Budget tracker (see [08 — Budgeting](08-budgeting.md)) |
| `/privacy` | `Privacy.razor` | Static content |
| `/terms` | `Terms.razor` | Static content |

### `StateLandingPage` — programmatic SEO

```razor
@page "/{StateSlug}-paycheck-calculator"
```

`OnParametersSet` resolves the slug through `StateMetadata.GetBySlug(StateSlug)`. On a hit, it
renders a state-specific `<title>`, meta description, canonical link, Open Graph / Twitter card
tags, and a `WebApplication` JSON-LD block — then delegates to the **same** `<Calculator />`
component as the home page, passing `InitialState="_info.State"` so the calculator opens
pre-selected to that state. On a miss, it renders a `noindex` "State Not Found" page with a link
back to the generic calculator, rather than a raw 404.

`StateMetadata` (in `Services/`) is a static in-memory catalog — `StateInfo` records keyed both by
slug and by `UsState` — holding the full name, slug, `<title>`, meta description, and a
state-specific withholding blurb for every landing page. The site's `/sitemap.xml` (registered
directly in `Program.cs` as a minimal-API `MapGet`, not a Razor page) iterates
`StateMetadata.All.OrderBy(s => s.Slug)` to emit one URL per state alongside the home page — so
the landing-page catalog and the sitemap are generated from the exact same source list and can
never drift apart.

One state landing page and one generic calculator, sharing one component, is the whole SEO
strategy: dozens of URLs, one implementation.

---

## `Calculator.razor` — the core page

At ~1,730 lines, this is the largest single file in the repository. It renders the entire
calculator experience: a four-tab input panel (Pay & Hours, Federal W-4, State, Deductions) with
a calculation-mode selector mirroring `CalculationMode` from the MAUI app (standard / gross-up /
bonus / self-employment), live results (hero summary, KPI row, doughnut chart, per-paycheck and
annual sub-tabs), "Show Your Work" explanation modals, an accuracy/sources modal, saved-paycheck
management with A/B comparison, and account/sync UI — all in one component, injected with the
calculator services directly:

```razor
@inject PayCalculator Calc
@inject AnnualProjectionCalculator ProjectionCalc
@inject GrossUpCalculator GrossUpCalc
@inject BonusCalculator BonusCalc
@inject SelfEmploymentCalculator SelfEmpCalc
@inject StateCalculatorRegistry StateRegistry
@inject IStateSchemaProvider SchemaProvider
@inject SessionPaycheckStore Store
@inject SessionBudgetStore BudgetStore
@inject CircuitAccountSession Account
@inject PaycheckApiClient Api
@inject PaycheckSyncService Sync
```

The `.razor.cs` code-behind partial (`Calculator.razor.cs`) holds only the small helper types that
need to be unit-testable from outside the render pipeline:

### `StateFieldVm` — schema-driven state input, rendered

The Blazor-side counterpart to MAUI's `StateFieldViewModel`, implementing the same schema
contract described in
[05 — The State Withholding Engine](05-state-withholding-engine.md#schema-driven-state-ui):

```csharp
internal sealed class StateFieldVm
{
    public StateFieldDefinition Def { get; }
    public string? SelectedOption { get; set; }   // Picker
    public string StringValue { get; set; } = ""; // Text/Integer/Decimal
    public bool BoolValue { get; set; }            // Toggle
    public string? ErrorMessage { get; set; }

    public void Validate() { /* required-field and type-parse checks per FieldType */ }
    public object? GetValue() { /* coerces to the CLR type the field type implies */ }
}
```

`RestoreFrom(old)` preserves user-entered values when the schema regenerates on a state switch —
the same "don't lose what the user typed for California just because they clicked Texas and back"
behavior MAUI implements with its `_stateFieldCache` dictionary.

Exposed via `InternalsVisibleTo` in the `.csproj` specifically so `StateFieldVmTest` in
`PaycheckCalculator.Tests` can exercise it directly, without spinning up a bUnit render harness.

### `DeductionVm`

A thin editable-row view model mapping directly to `Deduction`'s three independent
tax-base-reducing flags (see
[02 — Core Domain Model](02-core-domain-model.md#deduction)) — `ToDeduction()` is the one-way
conversion into the domain type the calculators actually consume.

---

## Shared components

`Components/Shared/`:

**`DoughnutChart.razor`** — renders the gross-pay breakdown as a hand-built SVG pie/doughnut,
computed entirely in C# rather than a JS charting library:

```csharp
double sweep = (double)(amount / total) * 2 * Math.PI;
var path = FormattableString.Invariant(
    $"M {cx:F2} {cy:F2} L {x1:F2} {y1:F2} A {r:F2} {r:F2} 0 {lg} 1 {x2:F2} {y2:F2} Z");
```

`FormattableString.Invariant` guards the SVG path data against locale-dependent decimal separators
— a comma-decimal culture would otherwise corrupt the `d` attribute. Five fixed slices
(Take-Home, Federal, State, FICA, Deductions) with literal hex colors chosen to match the CSS
design tokens, kept as literals specifically because they're written directly into an SVG `fill`
attribute rather than a CSS class.

**`ExplanationModal.razor`** — renders one `LineExplanation`: title, final amount, each
`ExplanationStep` (label/detail/formula/value), and — filtering the injected `Sources` list down
to just the citations whose `RuleId` appears in this line's `SourceRuleIds` — the structured
source citation(s), falling back to the legacy free-text `Reference` when no structured source
resolved. This is the UI-side mirror of the source-resolution logic described in
[07 — Explanations & Source Governance](07-explanations-and-source-governance.md).

**`AccuracySourcesModal.razor`** — the full accuracy-and-sources disclosure surface for the active
result.

**`HeroSummary.razor` / `KpiRow.razor` / `SetupChecklist.razor` / `ActivityLog.razor`** — smaller
presentational pieces of the results/onboarding experience under `Components/Calculator/`.

---

## Export, print, and JS interop

Export renderers live in `Services/Export/` and take the **domain types directly** — `PaycheckResult`,
`AnnualProjection`, `ComparisonRow` — mirroring the MAUI exporters' output format so a paycheck
exported from either front-end looks the same:

| Renderer | Produces |
|---|---|
| `PaycheckCsvRenderer` | CSV bytes: per-period summary + annual projection, with an optional A/B comparison section |
| `PaycheckPdfRenderer` | PDF bytes via the hand-rolled `PdfDocument` (507 lines) |
| `BudgetReportCsvRenderer` / `BudgetReportPdfRenderer` | The Pro-gated budget report, in each format |

`PdfDocument.cs` is the same dependency-free, purpose-built PDF writer pattern used on the MAUI
side (see [13 — The MAUI App](13-maui-app.md#pdf-generation-without-a-pdf-library)) — a second,
independent implementation in this project rather than a shared library, since Blazor's renderer
produces the PDF **server-side** while MAUI's produces it **on-device**.

The browser side of export/print is a tiny, focused JS file:

```javascript
// wwwroot/export.js
window.paycheckExport = {
    downloadFile: function (fileName, contentType, base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        const blob = new Blob([bytes], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url; anchor.download = fileName;
        document.body.appendChild(anchor); anchor.click(); document.body.removeChild(anchor);
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    print: function () { window.print(); }
};
```

The C# side generates bytes server-side, base64-encodes them, and calls this via
`IJSRuntime.InvokeVoidAsync`:

```csharp
private async Task ExportCsvAsync()
{
    var bytes = PaycheckCsvRenderer.Render(...);
    var base64 = Convert.ToBase64String(bytes);
    await JS.InvokeVoidAsync("paycheckExport.downloadFile", ExportFileName("csv"), "text/csv", base64);
}

private async Task PrintAsync() => await JS.InvokeVoidAsync("paycheckExport.print");
```

Print itself is native browser printing (`window.print()`), scoped by a `@media print`
stylesheet in `app.css` that isolates the results section so only the paycheck summary prints —
no server-side rendering is involved in the print path at all, only in CSV/PDF export.

---

## Styling

`wwwroot/app.css` is the entire design system — CSS custom properties (design tokens),
layout grid, form controls, the results panel, chart legend, modals, and the `@media print`
override. No CSS framework; the doughnut chart's inline colors are chosen to match this file's
token values (documented as a comment in `DoughnutChart.razor` itself, since the two can't share
values directly).

---

**Next:** [13 — The MAUI App](13-maui-app.md)
