# 13 — The MAUI App

`PaycheckCalculator.App` is the native front-end: Android, iOS, Mac Catalyst, and Windows from
one codebase, using .NET MAUI Shell and MVVM with CommunityToolkit.Mvvm source generators.

---

## Shell structure

`AppShell.xaml` is a five-tab `TabBar` with `FlyoutBehavior="Disabled"` — no hamburger menu, just
tabs, plus a shared `Shell.TitleView` brand bar ("PaycheckCalculator" / "Tax Year 2026") that
every page inherits:

| Tab | Page | Owns |
|---|---|---|
| Inputs | `InputsPage.xaml` | Pay & Hours / Federal / State / Deductions sub-tabs, each with a Calculate button |
| Results | `ResultsPage.xaml` | Per Paycheck / Annual sub-tab, doughnut chart, "Show Your Work", toolbar Print/Export PDF/Export CSV |
| Paychecks | `PaychecksPage.xaml` | Saved-paycheck list, A/B comparison, comparison-only export |
| Budget | `BudgetPage.xaml` | Budget methods, categories, transactions, recurring bills, savings goals, Pro-gated reports |
| Account | `AccountPage.xaml` | Sign-in / account creation / sync / server URL |

Every page and view model is registered `AddSingleton` in `MauiProgram.cs` — the app has exactly
one instance of each page and view model for its entire lifetime, which is what makes state
naturally persist across tab switches without any extra plumbing.

---

## MVVM with CommunityToolkit.Mvvm

The pattern used throughout: **pages are thin, view models own state and commands.** No tax math
ever appears in code-behind, converters, or drawables — `CalculatorViewModel.Calculate()` is the
only place a calculation is triggered, and it always goes through Core.

### Source-generator syntax

The project sets `<LangVersion>preview</LangVersion>` specifically to use `[ObservableProperty]`
on a **partial property** (rather than the older private-field style):

```csharp
[ObservableProperty] public partial decimal HourlyRate { get; set; }
```

The generator expands this into a full property with `SetProperty`-based change notification —
see [01 — Solution, Projects & Build](01-solution-and-build.md#sdk-pinning) for why the preview
language version is required.

`[RelayCommand]` on a method generates the corresponding `IRelayCommand`/`IAsyncRelayCommand`
property, so `private void Calculate()` becomes bindable as `CalculateCommand` in XAML.

### `PickerItem<T>` — the wrapper every enum-backed picker uses

```csharp
public record PickerItem<T>(T Value, string Text)
{
    public override string ToString() => Text;
}
```

Every `Picker` control in this app binds to a collection of `PickerItem<T>`, not the raw enum —
pairing the strongly-typed value with a human-readable display string
(`EnumDisplay.FederalFilingStatus(...)`, `EnumDisplay.PayType(...)`, etc.), while
`ToString()` is what MAUI's `Picker` actually renders. A `partial void OnSelected...Changed`
handler unwraps the picker selection back onto the real typed property:

```csharp
partial void OnSelectedFederalPickerItemChanged(PickerItem<FederalFilingStatus>? value)
{
    if (value != null) FederalFilingStatus = value.Value;
}
```

This same pattern repeats for filing status, pay frequency, calculation mode, pay type, salary
basis, and the state picker.

---

## `CalculatorViewModel` — the shared core of three tabs

At 1,298 lines, `CalculatorViewModel` is shared across Inputs, Results, and Paychecks — one view
model, three pages. It owns twelve collaborators injected in its constructor:

```csharp
public CalculatorViewModel(
    PayCalculator calc, GrossUpCalculator grossUp, BonusCalculator bonus,
    SelfEmploymentCalculator selfEmployment, AnnualProjectionCalculator annual,
    StateCalculatorRegistry stateRegistry, IStateSchemaProvider schemaProvider,
    IPdfExportService pdfExport, ICsvExportService csvExport, IPrintService printService,
    ISavedPaycheckStore store, ISyncCoordinator sync)
```

### `CalculationMode` — the same four-way toggle as Blazor

```csharp
public enum CalculationMode { Standard, GrossUp, Bonus, SelfEmployment }
```

Four `bool` properties (`IsStandardMode`, `IsGrossUpMode`, `IsBonusMode`, `IsSelfEmploymentMode`)
derived from it drive which input sections XAML shows, refreshed via explicit
`OnPropertyChanged` calls in the mode's partial changed-handler.

### `Calculate()` — validate, dispatch, persist

The single `[RelayCommand]` behind every Calculate button. Its structure is worth internalizing
because it is the reference implementation both front-ends' input handling follows:

1. **Bonus mode short-circuits immediately** — it "ignores hours/salary, W-4, and deductions, so
   it skips the paycheck-specific validation and state-schema fields below," and dispatches to a
   separate `CalculateBonus()` path.
2. **Every deduction needs a name** before proceeding (except in self-employment mode, which
   doesn't use deductions at all) — a genuinely blocking validation, not just a warning.
3. **Build `StateInputValues` from the live `StateFields` collection**, then run **two layers of
   state validation**: each `StateFieldViewModel.Validate()` (local parse/required checks) and
   the registered calculator's own `Validate(StateInputValues)` (cross-field business rules) —
   see [05 — The State Withholding Engine](05-state-withholding-engine.md#the-contract).
4. **Self-employment mode branches again here**, reusing the validated state inputs but its own
   engine and its own result mapping.
5. **Map view-model state → domain input** via `PaycheckInputMapper.Map(this, stateValues)`.
6. **Run `PaycheckInputValidator.Validate`** — the shared sanity checks from
   [02 — Core Domain Model](02-core-domain-model.md#paycheckinputvalidator).
7. **Any accumulated error blocks the calculation** — field errors, state errors, or input
   validator errors, checked together in one `if`.
8. **Dispatch to the right calculator** — gross-up clears `Projection` (it doesn't apply to a
   gross-up run); standard mode computes both the paycheck and its annual projection.
9. **Always land back on the Per Paycheck sub-tab** after any recalculation.
10. **Auto-save the result** via `SaveCurrentPaycheck`, and prefill the export file name from the
    paycheck name.
11. **Notify the export/print commands' `CanExecute`** so toolbar buttons enable immediately.

### `ShowExplanation` — "Show Your Work" as a native alert

```csharp
private async Task ShowExplanation(string keyName)
{
    if (!Enum.TryParse<ExplanationLineKey>(keyName, out var key)) return;
    var line = ResultCard.Explanation.Get(key);
    var sources = ResultCard.Explanation.Sources.Where(s => s.RuleId is not null && line.SourceRuleIds?.Contains(s.RuleId) == true).ToList();
    await Shell.Current.DisplayAlertAsync(line.Title, FormatExplanation(line, sources), "OK");
}
```

Same source-filtering logic as Blazor's `ExplanationModal.razor`
([12](12-blazor-web-app.md#shared-components)) — filter the resolved source citations down to the
ones this specific line actually cites — but rendered as a formatted plain-text native alert
instead of HTML, since MAUI's info-icon buttons pass the `ExplanationLineKey` name as a string
command parameter from XAML rather than binding a modal component.

---

## `StateFieldViewModel` — the MAUI half of schema-driven state UI

The counterpart to Blazor's `StateFieldVm` (see
[12 — The Blazor Web App](12-blazor-web-app.md#calculatorrazor--the-core-page)), built from the
same `StateFieldDefinition` and exposing the same `IsPicker`/`IsText`/`IsNumeric`/`IsToggle`
visibility flags that XAML's `DataTemplateSelector`-free binding switches on directly.

Its `Validate()` method carries a MAUI-specific defensive note worth knowing if you ever debug a
spurious "required" error on first load:

```csharp
private string? ValidatePicker()
{
    // When a Picker is inside a BindableLayout DataTemplate, MAUI may temporarily set
    // SelectedItem to null during binding initialization. Use the schema default when
    // SelectedOption is null so that the default selection doesn't block calculation.
    var effective = SelectedOption ?? Definition.DefaultValue?.ToString() ?? Definition.Options?.FirstOrDefault();
    ...
}
```

`GetResolvedValue()` applies the identical fallback when actually reading the value for
calculation — so a `Picker` control's binding-lifecycle quirk never leaks into a wrong or blocked
calculation.

`CalculatorViewModel._stateFieldCache` (a `Dictionary<UsState, Dictionary<string, object?>>`)
preserves what the user typed for one state when they switch to another and back — `RebuildStateFields()`
regenerates the `StateFields` collection from the newly selected state's schema, restoring cached
values where the field keys match.

---

## Mappers — the translation layer

Four static mapper classes, each doing exactly one direction of translation and nothing else, so
domain logic never leaks into a XAML binding:

| Mapper | Direction |
|---|---|
| `PaycheckInputMapper.Map(vm, stateValues)` | `CalculatorViewModel` → domain `PaycheckInput` |
| `ResultCardMapper.Map/MapGrossUp/MapBonus/MapSelfEmployment` | domain result → `ResultCardModel` |
| `AnnualProjectionMapper.Map` | domain `AnnualProjection` → `AnnualProjectionModel` |
| `SavedPaycheckSnapshotMapper.ToDto` / `.ToResultCard` | `ResultCardModel` ↔ `SavedPaycheckDto` |

`ResultCardMapper` is the one worth reading closely — it has to reconcile **four different result
shapes** (standard, gross-up, bonus, self-employment) into the single `ResultCardModel` the
Results page renders. Bonus and self-employment don't have annualized federal-taxable-income
figures at all, so `MapBonus` and `MapSelfEmployment` populate the "taxable income" rows with
whatever base each tax is actually computed against instead — the full bonus amount for bonus
mode, `NetEarningsSubjectToSeTax` for self-employment's FICA-equivalent row. Both set
mode-specific flags (`IsBonus`, `BonusStateUsesRegularMethod`, `IsSelfEmployment`,
`QuarterlyEstimates`) that the XAML uses to show or hide mode-specific sections.

`SavedPaycheckSnapshotMapper` explicitly documents what does **not** round-trip: *"Restored cards
have no 'Show Your Work' explanation (it is regenerated only for freshly calculated results)"* —
exactly the same design decision as `SavedPaycheckResultDto` omitting the explanation tree (see
[10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md#savedpaycheckresultdto-and-savedpaycheckresultmapper)).

---

## Presentation models

`Models/ResultCardModel.cs`, `AnnualProjectionModel.cs`, `ComparisonRow.cs` — plain `sealed class`es
decoupling the view layer from Core's domain types, adding display-only computed properties (mode
flags, formatted labels) that XAML binds to directly. They intentionally duplicate some fields
from `PaycheckResult` rather than exposing the domain type to the UI layer, keeping the dependency
direction one-way: ViewModels/Mappers know about Core; Views only know about presentation models.

---

## On-device storage

| Store | File | Notes |
|---|---|---|
| `JsonFilePaycheckStore` | `FileSystem.AppDataDirectory/saved-paychecks.json` | Implements `ISavedPaycheckStore`; semaphore-guarded read-modify-write; a corrupt file is moved aside rather than crashing app startup |
| `JsonFileBudgetStore` | `FileSystem.AppDataDirectory/budgets.json` | Implements `IBudgetStore`; same pattern |

Both are registered `AddSingleton`, so they hold the true in-memory-plus-on-disk state for the
app's whole lifetime — this is what makes saved paychecks and budgets **work fully offline, with
no account required**, unlike Blazor's circuit-scoped, account-required-for-persistence approach.

---

## Sync

`SyncCoordinator` (implementing `ISyncCoordinator`) is MAUI-specific because it depends on
`Connectivity` and `MainThread` — covered in detail in
[10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md#maui-only-orchestration-synccoordinator).
`CalculatorViewModel` subscribes to `_sync.SyncCompleted` in its constructor so the saved-paycheck
list refreshes whenever a sync lands, from any trigger.

`AccountViewModel` (227 lines) owns the Account tab: login, register, logout, sync-now, export
account data, delete local data, delete account, and links to the privacy policy / terms. Nearly
every command follows the same `RunAsync(async () => ...)` wrapper pattern that centralizes
busy-state and error handling around the underlying `PaycheckApiClient` call.

`PreferencesApiBaseAddressProvider` stores the user-editable server URL in MAUI `Preferences`;
`SecureStorageTokenStore` persists tokens in the platform's secure storage so a signed-in session
survives an app restart (unlike Blazor, where sign-in state is circuit-only).

---

## Export services

Three parallel export/print stacks under `Services/`, each with an `I*Service` +
implementation + a platform launcher:

```text
Services/Pdf/       IPdfExportService, PdfExportService, PaycheckPdfRenderer, PdfDocument, IPdfViewerLauncher, PdfViewerLauncher
Services/Csv/       ICsvExportService, CsvExportService, PaycheckCsvRenderer, BudgetReportCsvRenderer, ICsvViewerLauncher, CsvViewerLauncher
Services/Printing/  IPrintService, PrintService, IPrintLauncher, PrintLauncher
```

The renderers take a `ResultCardModel` plus an optional `AnnualProjectionModel`/`ComparisonRow`s
and return raw bytes; the `*ExportService`s just persist those bytes under a name and hand off to
a viewer launcher (opening the default PDF/CSV app) or, for printing, the native print dialog.
Every export covers **both** the per-period summary **and** the annual projection in one document,
appending the A/B comparison section when two saved paychecks are selected on the Paychecks page.

File names are sanitized through `FileNameSanitizer.Sanitize`, which replaces any character
`Path.GetInvalidFileNameChars()` flags with a hyphen and falls back to `"Paycheck-Summary"` when
the cleaned result is empty — so a paycheck named `"Job / Side Gig"` still produces a valid file
name instead of failing to save.

### PDF generation without a PDF library

`Services/Pdf/PdfDocument.cs` (101 lines) is a minimal, dependency-free PDF writer — "deliberately
not a general-purpose PDF library" — covering exactly what the export needs: built-in Helvetica
fonts, text, filled rectangles, and embedded baseline-JPEG images via the `DCTDecode` filter.

```csharp
public int Reserve();                       // reserve a 1-based object number, body filled in later
public int Add(byte[] body);                 // append an object with a known body now
public void Set(int id, string body);        // fill in a previously reserved object
```

The reserve/fill-later pattern exists because a PDF's catalog and page tree reference each other
circularly by object number — reserving numbers up front lets the builder wire up those
cross-references before every object's final content is known, and byte offsets for the
cross-reference table are computed once, during `Build()`, guaranteeing the output is always
self-consistent. `PaycheckPdfRenderer` (497 lines) is the layout logic on top of this primitive —
tables, headers, section spacing — built entirely from `PdfDocument`'s low-level drawing calls.

`PaycheckCalculator.Blazor` ships an **independent second implementation** of this exact same
pattern in its own `Services/Export/PdfDocument.cs`, because Blazor's renderer runs server-side
while MAUI's runs on-device — see [12 — The Blazor Web App](12-blazor-web-app.md#export-print-and-js-interop).

---

## The doughnut chart

`Controls/DoughnutChartDrawable.cs` implements `IDrawable`, MAUI's `GraphicsView` drawing
contract — a hand-rolled arc-segment renderer (`MinArcSegments = 8`, `DegreesPerSegment = 3f`)
building smooth arcs from small angular steps, colored from a fixed palette that mirrors
`Resources/Styles/Colors.xaml`'s token ramp:

```csharp
// Mirrors the token ramp in Resources/Styles/Colors.xaml. Kept as literals because
// IDrawable has no access to the XAML resource dictionary at draw time.
private static readonly Color[] SliceColors = { ... };
```

The same constraint Blazor's `DoughnutChart.razor` notes for its own hard-coded hex colors — a
drawing surface (SVG `fill` attribute there, `IDrawable`/`ICanvas` here) that can't reach into the
app's design-token system, so the palette is duplicated as literals and kept in sync by comment
discipline rather than a shared source.

Only slices with a positive value are added, so a no-income-tax state's paycheck renders a chart
with no empty "State Tax: $0" wedge.

---

## Formatting helpers

**`DecimalFormatBehavior`** — a MAUI `Behavior<Entry>` attached to numeric input fields via
`IsCurrency`/`IsPercentage` bindable properties. On focus, it strips formatting back to a plain
editable number (`"$1,234.56"` → `"1234.56"`); on blur, it reformats
(`"C2"` for currency, `"0.00%"`-shaped for percentage) — the classic "format on blur, raw on
focus" pattern for money entry fields, implemented once and reused across every currency/percent
`Entry` in the app rather than duplicated per field.

**`EnumDisplay`** — the single source of human-readable labels for every enum surfaced in a
picker (`FederalFilingStatus`, `PayType`, `SalaryBasis`, `CalculationMode`, `DeductionType`,
`UsState` full names, etc.), keeping display strings out of both the domain enums themselves and
individual XAML files.

**Converters** (`Helpers/`) — `GreaterThanZeroConverter`, `InvertBoolConverter`,
`StringToBoolConverter`: small, single-purpose `IValueConverter`s for the visibility/enablement
bindings XAML needs (e.g. `IsVisible="{Binding IsPro, Converter={StaticResource InvertBool}}"`
from the budget-report upsell card).

---

## Comparing the two front-ends' calculator flow

| | MAUI | Blazor |
|---|---|---|
| Where state lives | `CalculatorViewModel` (`AddSingleton`, app lifetime) | `Calculator.razor` fields (circuit lifetime) |
| Input → domain type | `PaycheckInputMapper.Map` | Built inline in the component |
| Result → display type | `ResultCardMapper` → `ResultCardModel` | Domain `PaycheckResult` used directly |
| State field UI | `StateFieldViewModel` + `StateFieldViewModel.Validate()` | `StateFieldVm` + `.Validate()` (same contract, separate implementation) |
| Explanation UI | Native `DisplayAlert`, plain text | `ExplanationModal.razor`, HTML |
| Chart | `DoughnutChartDrawable` (`IDrawable`) | `DoughnutChart.razor` (inline SVG) |
| PDF | `PdfDocument` + `PaycheckPdfRenderer` (on-device) | `PdfDocument` + `PaycheckPdfRenderer` (server-side, separate implementation) |
| Persistence | `JsonFilePaycheckStore` (always-on, on-device) | `SessionPaycheckStore` (circuit memory, mirrored to browser `localStorage`) |

Both front-ends deliberately **duplicate presentation-layer code** (mappers, chart renderers, PDF
writers) while sharing **100% of the calculation logic** through Core — the layering boundary
described in [01 — Solution, Projects & Build](01-solution-and-build.md#dependency-graph-and-layering-rules)
holds exactly at that line.

---

**Next:** [14 — The Test Suite](14-test-suite.md)
