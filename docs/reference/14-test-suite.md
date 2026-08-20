# 14 — The Test Suite

`PaycheckCalculator.Tests` — 83 files, ~1,388 `[Fact]`/`[Theory]` attributes, xUnit. It transitively
builds and exercises Core, Shared, Api, and Blazor (via the aliased reference described in
[01 — Solution, Projects & Build](01-solution-and-build.md#two-notable-cross-project-mechanics)),
all without the MAUI workload — which is exactly why CI can run it on plain `ubuntu-latest`.

```bash
dotnet test PaycheckCalculator.Tests
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~CaliforniaPercentageCalculatorTest"
dotnet test PaycheckCalculator.Tests --filter "FullyQualifiedName~OklahomaOw2RoundingTest&DisplayName~RoundsToWholeDollar"
```

---

## How most calculator tests are structured

The overwhelming majority of tests construct their calculator **directly from a data file**,
bypassing `AddPaycheckCalculatorCore` and the DI container entirely:

```csharp
private static PayCalculator CreateCalculator()
{
    var registry = new StateCalculatorRegistry();
    var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
    registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
    var fica = new FicaCalculator();
    var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
    return new PayCalculator(registry, fica, new Irs15TPercentageCalculator(fedJson));
}
```

The JSON files are present because `PaycheckCalculator.Tests.csproj` content-links every data
file from Core, copying them next to the test binary (see
[09 — Tax Data Files](09-tax-data-files.md#4-paycheckcalculatortestscsproj--content-linked-for-the-test-binary)).
Most tests register **only the one or two states** they need, not the full 51 — a deliberately
minimal registry keeps each test focused and fast.

### `TestSchemas` — bridging the schema-provider API to old test call sites

```csharp
public static class TestSchemas
{
    public static IStateSchemaProvider Provider { get; } = BuildProvider();   // built once from Schemas/*.json next to the test binary
}

public static class StateCalculatorSchemaTestExtensions
{
    public static IReadOnlyList<StateFieldDefinition> GetInputSchema(this IStateWithholdingCalculator calc)
        => TestSchemas.Provider.GetSchema(calc.State);
}
```

When schemas moved from being a method on `IStateWithholdingCalculator` to standalone JSON (see
[05 — The State Withholding Engine](05-state-withholding-engine.md#the-contract)), this extension
method let every pre-existing `calc.GetInputSchema()` call site in the test suite keep compiling
unchanged, now backed by the JSON provider instead of a virtual method. `TestSchemas.Provider` is
a `static readonly` singleton for the whole test run, built once from whatever `Schemas/*.json`
files were copied next to the test binary.

---

## The golden-vector rule

**Expected values are explicit numeric literals taken from the rule or published table — never
recomputed with production helpers.** This is the rule that makes the suite capable of catching a
regression at all: a test that calls the same calculator to generate its own expected value would
pass even after the calculator broke, because it would simply be comparing broken output to
broken output.

`VerifiedCalculationCorpusTest` is the canonical example, with every literal traced to its source
in a comment:

```csharp
[Fact]
public void Pub15T2026_Worksheet1A_BiweeklyGoldenVector()
{
    // Single, $3,000 biweekly:
    // $3,000 × 26 = $78,000; Worksheet 1A line 1g subtracts $8,600;
    // $69,400 falls in the $57,900 bracket:
    // ($5,800 + ($69,400 - $57,900) × 22%) / 26 = $320.38.
    var actual = calculator.CalculateWithholding(3_000m, PayFrequency.Biweekly, w4);
    Assert.Equal(320.38m, actual);
}
```

Every one of the 44 dedicated state calculators has a matching `<StateName>WithholdingCalculatorTest.cs`
following the same pattern — bracket boundaries, allowance/exemption handling, extra withholding,
pre-tax deduction effects, rounding edges, and the state's own documented quirks (California's
3-cent adjustment, Oklahoma's whole-dollar rounding — see
[05](05-state-withholding-engine.md#documented-intentional-quirks)).

---

## Architecture-conformance tests

Some tests exist to protect an invariant of the *system*, not the value of one calculation.

### `TaxSourceManifestTest`

Loads the real manifest through the real `AddPaycheckCalculatorCore` composition and asserts the
governance rules from [07](07-explanations-and-source-governance.md) hold on the live data —
that every rule ID is unique, every rule has a valid HTTPS URL and non-empty publication title,
every state has exactly one `RegularWithholding` rule, and the required federal scopes
(`FederalWithholding`, `SocialSecurityMedicare`, `AdditionalMedicare`, `SupplementalWithholding`,
`SelfEmploymentTax`, `EstimatedPayments`) all exist under jurisdiction `"US"`. This is a **live
data test**, not a unit test of the loader logic — it fails the moment the manifest itself
regresses, independent of any calculator's correctness.

### `StateWithholdingArchitectureTest`

A large file (854 lines, the biggest in the suite) importing every one of the 44 dedicated state
calculator namespaces at once. It asserts structural properties across the **entire** state
engine simultaneously — every state resolves to a working calculator, every calculator's schema
round-trips through validation correctly, and the shape of `IStateWithholdingCalculator` is
honored uniformly. This is where a change like "the registry is missing a state" or "a
calculator's `Validate` throws instead of returning errors" gets caught, rather than being left to
whichever individual state test happens to exercise that path.

### `StateInputValuesTest` (inside `StateWithholdingArchitectureTest.cs`)

Directly exercises `StateInputValues.GetValueOrDefault<T>`'s coercion rules — typed match,
fallback on missing key, string→decimal/int conversion, fallback on invalid conversion,
case-insensitive key lookup — the exact behavior every state calculator's `Calculate` depends on
implicitly. See [05](05-state-withholding-engine.md#stateinputvalues).

### `JsonStateSchemaProviderTest`

Unit tests the schema loader itself — parsing, default-value decoding per field type, unknown
field type rejection, missing-schema-returns-empty behavior.

### `StateFieldVmTest`

```csharp
extern alias blazor;
using blazor::PaycheckCalculator.Blazor.Components.Pages;
```

The test that justifies the whole `InternalsVisibleTo` + extern-alias mechanism described in
[01](01-solution-and-build.md#two-notable-cross-project-mechanics). Its docstring states the exact
property it protects:

> Decimal/integer parsing must consistently use `CultureInfo.InvariantCulture` in both
> `Validate()` and `GetValue()` so a value that validates successfully is guaranteed to parse to
> the same value used in calculations, regardless of the current thread culture (e.g. locales
> where ',' is the decimal separator and '.' is a thousands separator).

It runs assertions under a temporarily-switched culture (de-DE, comma-decimal) specifically to
catch the class of bug where a field *validates* under one culture's parsing rules but then
*calculates* incorrectly because a different code path used the ambient culture instead of
invariant — a real, subtle bug class for any app that parses user-typed numbers.

### `TaxYearVersioningTest`

Exercises `TaxYearSupport` and confirms every input type defaults to the supported year, every
result type echoes it back, and — critically — that `PaycheckExplanation.Sources` correctly
aggregates and deduplicates citations across lines (two lines sharing the exact same `Reference`
string collapse to one citation; `PaycheckExplanation.Empty` has zero sources). This file
predates the full `TaxSourceCatalog` machinery for some of its assertions and constructs
`LineExplanation`s with legacy free-text `Reference`s directly, rather than going through rule
IDs — a good illustration of the fallback path described in
[07](07-explanations-and-source-governance.md#the-show-your-work-model).

---

## API integration tests

`SyncApiTest` hosts the **entire ASP.NET Core pipeline in-process** via
`WebApplicationFactory<Program>`, backed by a shared SQLite connection instead of PostgreSQL (see
[11 — The Sync API](11-sync-api.md#postgresql-vs-sqlite)). It exercises the system as a real HTTP
client would:

- `Sync_WithoutToken_Returns401` — authorization is actually enforced, not just configured.
- `Register_ThenLogin_IssuesBearerToken` — the full Identity register → login → bearer-token
  round trip.
- `FirstSync_EchoesPushedPaychecks` — a fresh account's first sync returns exactly what it pushed.
- `SecondClient_WithNewerEntry_WinsAcrossSync` — **two separate signed-in clients on the same
  account**, simulating two devices; the client with the later timestamp wins, end to end through
  real HTTP requests and real server-side merge and persistence — not just a unit test of
  `SavedPaycheckMerger` in isolation.
- `Tombstone_PropagatesDelete` — a delete on one sync round trip is honored by a subsequent sync,
  confirming tombstones actually persist and take effect server-side.

The docstring calls out the test that matters most for correctness of the whole sync feature:
*"most importantly, that a snapshot's `StateInputValues` survives a full server round-trip
intact"* — proving `StateInputValuesJsonConverter` and `PaycheckJson`'s shared configuration
(see [10](10-shared-contracts-and-sync.md#stateinputvaluesjsonconverter)) actually work end to end
through real HTTP serialization, not just in a converter unit test.

---

## Front-end-adjacent tests

Because Blazor's export renderers, `Calculator.razor`'s internal helper types, and the state-field
view-model logic are pure C# reachable via `InternalsVisibleTo`, they are unit-tested directly
from `PaycheckCalculator.Tests` with no bUnit render harness or browser required:

| Test file | Covers |
|---|---|
| `PaycheckCsvRendererTest` | CSV export shape and content (Blazor's renderer) |
| `PaycheckPdfRendererTest` | PDF byte generation via `PdfDocument` (Blazor's renderer) |
| `PaycheckComparisonTest` | `PaycheckComparison.BuildRows` — the A/B comparison logic |
| `StateFieldVmTest`, `StatePickerAndDecimalFixTest` | Blazor page-internal helper correctness |

The MAUI app is **not** covered by this suite at all — it can't be, since it doesn't build on
Linux/CI. Its mappers and view models are exercised only by manual testing on-device (or via a
future MAUI-specific test target, which does not currently exist).

---

## Domain and pipeline tests (representative, not exhaustive)

| Test file | Covers |
|---|---|
| `PayCalculatorExplanationTest` | The full explanation-tree assembly in `PayCalculator.BuildExplanation` — line order, conditional lines, the zero-state-tax special case |
| `ReducesFederalTaxableIncomeTest` | Every combination of `Deduction`'s three tax-base flags, verified against actual computed wage bases |
| `DeductionAmountTypeTest` | `EffectiveAmount` for `Dollar` vs `Percentage` |
| `SalaryPayTypeTest` | `PayType.Salary` × `SalaryBasis` combinations |
| `GrossUpCalculatorTest` | Convergence, the net-≥-target guarantee, behavior with percentage deductions and FICA caps |
| `BonusCalculatorTest` | All four `StateSupplementalMethod`s, FICA on bonuses, the regular-method caveat |
| `SelfEmploymentCalculatorTest` | 92.35% base, wage-base cap, Additional Medicare crossing, exact quarterly-sum reconciliation |
| `AnnualProjectionCalculatorTest` | Annualization, YTD, paycheck-number clamping, over/under sign convention |
| `HourlySalaryCalculatorTest` | Both conversion directions, custom hours/weeks |
| `HourlySalaryValidationTest` | Converter input validation surfaced by both front-ends |
| `BrowserBackedStoreTest` | Blazor `localStorage` persistence: survival across circuits, prerender fallback, corrupt/newer payloads |
| `PaycheckSnapshotJsonTest` | `SavedPaycheckDto` round-trips through `PaycheckJson.Options` intact |
| `SavedPaycheckMergerTest`, `BudgetMergerTest` | The three-level last-write-wins tie-break, for both paycheck and budget/transaction/bill/goal merges |
| `BudgetCalculatorTest`, `BudgetReportCalculatorTest`, `RecurringBillTest`, `SavingsGoalTest` | Budgeting engine — see [08](08-budgeting.md) |

Every one of the 44 states also has its own `<StateName>WithholdingCalculatorTest.cs`, not
enumerated individually here — see [05 — The State Withholding Engine](05-state-withholding-engine.md#full-coverage-table)
for the full jurisdiction list.

---

## Writing a new test

Following the conventions already established across the suite:

1. **Name it for the scenario**, not the method — `RoundsToWholeDollar`,
   `SecondClient_WithNewerEntry_WinsAcrossSync`, `Pub15T2026_Worksheet1A_BiweeklyGoldenVector`.
2. **Use an explicit numeric expected value** sourced from the actual rule/table/publication —
   include the arithmetic in a comment so a future reader (or reviewer during an accuracy
   correction, per [07](07-explanations-and-source-governance.md#the-eight-step-correction-process))
   can verify it independently of the code.
3. **Construct the minimal calculator you need** directly from the data file next to the test
   binary — don't reach for the full DI container unless the test is specifically about
   composition (like `TaxSourceManifestTest`).
4. **Cover the edges**: bracket boundaries, exemption/allowance handling, extra withholding,
   pre-tax deduction interaction, rounding, and any state-specific documented quirk.
5. **When you change a calculator, update its corresponding test file** — the two are meant to
   move together, in the same change, always.

---

**Next:** [15 — Conventions & Workflows](15-conventions-and-workflows.md)
