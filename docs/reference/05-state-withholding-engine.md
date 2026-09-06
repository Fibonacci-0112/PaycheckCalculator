# 05 — The State Withholding Engine

All 50 states plus the District of Columbia are supported. Each jurisdiction's rules live in
exactly one place, and the UI fields each jurisdiction needs are declared as **data**, not code.

---

## The contract

`Core/Tax/State/IStateWithholdingCalculator.cs`:

```csharp
public interface IStateWithholdingCalculator
{
    UsState State { get; }
    IReadOnlyList<string> Validate(StateInputValues values);
    StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values);
}
```

Deliberately small. Note what is *absent*: there is no `GetInputSchema()`. Schemas were moved out
of C# into `Data/Schemas/<state>.json` and are served by `IStateSchemaProvider`, so a
year-over-year UI change is a data edit.

### `CommonWithholdingContext`

The universal payroll facts every state might need — a positional `record`:

```csharp
public sealed record CommonWithholdingContext(
    UsState State,
    decimal GrossWages,                            // this period, before deductions
    PayFrequency PayPeriod,
    int Year,
    decimal PreTaxDeductionsReducingStateWages = 0m,
    decimal FederalWithholdingPerPeriod = 0m);
```

`FederalWithholdingPerPeriod` exists for states — Alabama most notably — that subtract federal
withholding when computing state taxable income. It is supplied already rounded to the cent so a
user can reproduce the worksheet by hand.

### `StateInputValues`

The per-state input bag:

```csharp
public sealed class StateInputValues : Dictionary<string, object?>
{
    public StateInputValues() : base(StringComparer.OrdinalIgnoreCase) { }
    public T GetValueOrDefault<T>(string key, T fallback = default!) { ... }
}
```

Case-insensitive keys matching `StateFieldDefinition.Key`. `GetValueOrDefault<T>` first tries a
direct `raw is T` match, then falls back to `Convert.ToDecimal` / `ToInt32` / `ToBoolean` for
numeric and boolean targets, and returns the fallback on `FormatException`,
`InvalidCastException`, or `OverflowException`. A calculator therefore never has to null-check or
try-parse.

This coercion behavior is exactly why `StateInputValuesJsonConverter` in Shared materializes
values as **real CLR primitives rather than `JsonElement`** — a `JsonElement` would fail `raw is T`
for every type and silently fall through to the fallback for strings and booleans. See
[10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md).

### `StateWithholdingResult`

The normalized return shape, no matter how idiosyncratic the state's internals:

```csharp
public sealed class StateWithholdingResult
{
    public decimal TaxableWages { get; init; }
    public decimal Withholding { get; init; }

    public decimal DisabilityInsurance { get; init; }
    public string  DisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public string? Description { get; init; }

    public IReadOnlyList<ExplanationStep>? WithholdingSteps { get; init; }
    public string? WithholdingReference { get; init; }
    public IReadOnlyList<ExplanationStep>? DisabilityInsuranceSteps { get; init; }
    public string? DisabilityInsuranceReference { get; init; }
}
```

The four explanation members are **opt-in**. A calculator that supplies `WithholdingSteps` gets
its own worksheet rendered verbatim; one that does not gets a generic wage-base + amount
breakdown synthesized by `PayCalculator`.

---

## The registry

`Core/Tax/State/StateCalculatorRegistry.cs` — a `Dictionary<UsState, IStateWithholdingCalculator>`
with a cached sorted key list:

```csharp
public void Register(IStateWithholdingCalculator calculator)
{
    _calculators[calculator.State] = calculator;   // last registration wins
    _sortedStates = null;                          // invalidate the cache
}

public IStateWithholdingCalculator GetCalculator(UsState state) =>
    _calculators.TryGetValue(state, out var calc)
        ? calc
        : throw new NotSupportedException($"State withholding calculator for {state} has not been registered.");

public IReadOnlyList<UsState> SupportedStates =>
    _sortedStates ??= _calculators.Keys.OrderBy(s => s.ToString()).ToList();
```

`SupportedStates` is what both front-ends bind their state picker to — the picker is generated
from the registry, never hand-maintained.

---

## Schema-driven state UI

### The field definition

`Core/Tax/State/StateFieldDefinition.cs`:

```csharp
public enum StateFieldType { Text, Integer, Decimal, Toggle, Picker }

public sealed class StateFieldDefinition
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required StateFieldType FieldType { get; init; }
    public bool IsRequired { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<string>? Options { get; init; }   // Picker only
}
```

For `Picker` fields each option string is **both the display text and the stored value**, which
keeps the JSON, the UI, and `Validate` all comparing the same literals.

### The JSON

One file per jurisdiction under `Core/Data/Schemas/`. California:

```json
{
  "state": "CA",
  "fields": [
    {
      "key": "FilingStatus", "label": "Filing Status", "type": "Picker",
      "required": true, "default": "Single",
      "options": ["Single", "Married", "Head of Household"]
    },
    { "key": "RegularAllowances", "label": "Regular Allowances (DE 4 Line 1)", "type": "Integer", "default": 0 },
    { "key": "EstimatedDeductionAllowances", "label": "Estimated Deduction Allowances (DE 4 Line 2)", "type": "Integer", "default": 0 },
    { "key": "AdditionalWithholding", "label": "Extra Withholding", "type": "Decimal", "default": 0 }
  ]
}
```

Labels cite the actual state form line ("DE 4 Line 1") so a user can match the app to the paper
in front of them.

### The provider

`JsonStateSchemaProvider` parses every schema once at construction and caches the results.
Parsing is strict where it should be and lenient where it helps:

```csharp
if (!Enum.TryParse<StateFieldType>(raw, ignoreCase: true, out var ft))
    throw new InvalidOperationException($"Unknown field type '{raw}' for {state}.{key} in schema JSON.");
```

An unknown field type is a hard failure at startup. Meanwhile the reader allows comments
(`ReadCommentHandling = Skip`) and trailing commas, and `DecodeDefault` coerces each `default`
value to the CLR type implied by the field type. A missing schema file yields an empty list, not
an exception.

`GetOptions(state, fieldKey)` is the second half of the contract — it lets a calculator's
`Validate` check user input against the **JSON-defined** option set instead of a duplicated
hard-coded array:

```csharp
// PercentageMethodWithholdingAdapter
_filingStatusOptions = schemaProvider.GetOptions(state, "FilingStatus");
...
if (_filingStatusOptions.Count > 0 && !_filingStatusOptions.Contains(status))
    errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");
```

### The four things to keep in sync

When you add or change a state input, all four must move together:

1. **Schema** — `Core/Data/Schemas/<state>.json`
2. **Validation** — that state's `Validate(StateInputValues)`
3. **Calculation** — the `GetValueOrDefault<T>` read in `Calculate`
4. **Tests** — the state's `*WithholdingCalculatorTest.cs`

The UI needs no change at all: both front-ends render whatever the schema declares. **Never
hard-code a per-state control** in XAML or Razor when the schema can express it.

---

## Calculator categories

### 1. Dedicated per-state calculators (44 classes)

Each lives in `Core/Tax/<StateName>/<StateName>WithholdingCalculator.cs` and encodes that state's
published withholding formula. Most are `CodedFormula` implementations — annualize, subtract a
filing-status standard deduction, subtract per-allowance amounts, apply graduated brackets,
subtract allowance credits, de-annualize, add extra withholding — with the state's own constants
and quirks.

Six are **`JsonTable`** implementations backed by their own data file and a helper class:

| State | Data file | Helper | Wrapper |
|---|---|---|---|
| Arkansas | `ar_withholding_2026.json` | `ArkansasFormulaCalculator` | `ArkansasWithholdingCalculator` |
| California | `ca_method_b_2026.json` | `CaliforniaPercentageCalculator` | `CaliforniaWithholdingCalculator` |
| Colorado | `co_dr0004_2026.json` | — (calculator loads directly) | `ColoradoWithholdingCalculator` |
| Connecticut | `connecticut_withholding_2026.json` | — | `ConnecticutWithholdingCalculator` |
| Oklahoma | `ok_ow2_2026_percentage.json` | `OklahomaOw2PercentageCalculator` | `OklahomaWithholdingCalculator` |

(Five files; California and Arkansas split helper from wrapper, Colorado and Connecticut are
registered as both the helper singleton and the registry entry.)

Arizona is the sole **`PercentageElection`** implementation: Form A-4 lets the employee elect a
withholding percentage (0.5%–3.5%) applied to gross taxable wages, defaulting to 2.0% when no
A-4 is on file.

### 2. `NoIncomeTaxWithholdingAdapter` (7 states)

For jurisdictions with no individual income tax **and** no employee payroll assessment:

```csharp
public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    => new()
    {
        TaxableWages = 0m,
        Withholding = 0m,
        Description = "No state income tax",
        WithholdingSteps = StateExplanationSteps.NoIncomeTax(State),
        WithholdingReference = $"{State} levies no state personal income tax (2026)."
    };
```

Registered for **AK, FL, NV, NH, SD, TN, TX**. `Validate` always returns an empty list and the
schema files for these states declare no fields.

### 3. Dedicated no-income-tax calculators (WA, WY)

Washington and Wyoming have no income tax but get their own classes so state-specific payroll
assessments stay in their own module:

- **`WashingtonWithholdingCalculator`** — zero income tax, plus the mandatory **WA Cares Fund**
  long-term-care premium at **0.58% of all gross wages, with no cap and before any pre-tax
  deductions**, surfaced on the `DisabilityInsurance` line labeled
  `"WA Cares Fund (Long-Term Care)"`. A `WaCaresExempt` toggle in the schema suppresses it for
  employees holding a DSHS-approved exemption. Source: RCW 50B.04.080.
- **`WyomingWithholdingCalculator`** — zero income tax, empty schema, no assessment. It exists so
  Wyoming is not lumped into the shared adapter, keeping room for future state-specific rules.

### 4. The generic percentage-method calculator

`PercentageMethodStateTaxCalculator` + `PercentageMethodWithholdingAdapter` implement the
textbook annualized percentage method:

1. Taxable wages = gross − state-reducing pre-tax deductions
2. Annualize (× periods/year)
3. Subtract the filing-status standard deduction
4. Subtract `Allowances × AllowanceAmount`
5. Apply graduated brackets
6. Subtract `Allowances × AllowanceCreditAmount`
7. De-annualize and round to the cent
8. Add per-period additional withholding

**It is not wired to any production state today.** `StateTaxConfigs2026.Configs` is an empty
dictionary — every entry has been migrated to a dedicated calculator, and the file now holds a
long block of comments recording, state by state, which calculator took over and with what
constants. That commentary is genuinely useful as a quick reference (Georgia's flat 5.19% with
G-4 statuses; Maryland's flat $3,400 standard deduction prorated per payroll period; Montana's 20% standard
deduction with min/max bounds; and so on).

The class is retained because `PercentageMethodStateTaxCalculatorTest` exercises the generic
algorithm directly, and it remains the natural base for a future state.

> **Trap if you ever re-wire it.** Its private `GetPayPeriods` covers only eight frequencies and
> **throws `ArgumentOutOfRangeException` for `Weekly53` and `Biweekly27`**, unlike
> `PayPeriods.PerYear`. Fix that before registering it for a real state.

---

## Coverage reconciliation

The numbers line up as follows, and the code enforces it:

```text
44 dedicated calculator classes   (one per jurisdiction, incl. WA and WY)
+ 7 NoIncomeTaxWithholdingAdapter registrations (AK FL NV NH SD TN TX)
──────────────────────────────────
= 51 jurisdictions = 50 states + DC
```

In `AddPaycheckCalculatorCore` that is 44 explicit `Register(...)` calls (42 inline `new`, plus
`coCalc` and `ctCalc` registered from variables since they are also needed as standalone
singletons), then a loop over the seven no-tax states, then a loop over the empty
`StateTaxConfigs2026.Configs`.

The guarantee is not left to inspection. `TaxSourceCatalog.Load` refuses to load a manifest
lacking `RegularWithholding` coverage for any `UsState`, and:

```csharp
public void ValidateCalculatorRegistrations(StateCalculatorRegistry registry)
{
    foreach (var state in Enum.GetValues<UsState>())
    {
        var rule = Rules.Single(c => c.AppliesTo(state) && c.Scope == TaxRuleScope.RegularWithholding);
        var actual = registry.GetCalculator(state).GetType().Name;
        if (!string.Equals(rule.CalculatorClass, actual, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Tax source rule '{rule.Id}' names calculator '{rule.CalculatorClass}', but '{actual}' is registered.");
    }
}
```

Three failure modes are caught at startup: a missing registration (`GetCalculator` throws), a
missing or duplicated manifest rule (`Single` throws), and a rename that desyncs the manifest
from the code (the string comparison throws).

---

## Full coverage table

From `tax_source_manifest_2026.json`, scope `RegularWithholding` — 51 rules, one per jurisdiction.

| | Implementation type | Calculator |
|---|---|---|
| **AK** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **AL** | `CodedFormula` | `AlabamaWithholdingCalculator` |
| **AR** | `JsonTable` | `ArkansasWithholdingCalculator` |
| **AZ** | `PercentageElection` | `ArizonaWithholdingCalculator` |
| **CA** | `JsonTable` | `CaliforniaWithholdingCalculator` |
| **CO** | `JsonTable` | `ColoradoWithholdingCalculator` |
| **CT** | `JsonTable` | `ConnecticutWithholdingCalculator` |
| **DC** | `CodedFormula` | `DistrictOfColumbiaWithholdingCalculator` |
| **DE** | `CodedFormula` | `DelawareWithholdingCalculator` |
| **FL** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **GA** | `CodedFormula` | `GeorgiaWithholdingCalculator` |
| **HI** | `CodedFormula` | `HawaiiWithholdingCalculator` |
| **IA** | `CodedFormula` | `IowaWithholdingCalculator` |
| **ID** | `CodedFormula` | `IdahoWithholdingCalculator` |
| **IL** | `CodedFormula` | `IllinoisWithholdingCalculator` |
| **IN** | `CodedFormula` | `IndianaWithholdingCalculator` |
| **KS** | `CodedFormula` | `KansasWithholdingCalculator` |
| **KY** | `CodedFormula` | `KentuckyWithholdingCalculator` |
| **LA** | `CodedFormula` | `LouisianaWithholdingCalculator` |
| **MA** | `CodedFormula` | `MassachusettsWithholdingCalculator` |
| **MD** | `CodedFormula` | `MarylandWithholdingCalculator` |
| **ME** | `CodedFormula` | `MaineWithholdingCalculator` |
| **MI** | `CodedFormula` | `MichiganWithholdingCalculator` |
| **MN** | `CodedFormula` | `MinnesotaWithholdingCalculator` |
| **MO** | `CodedFormula` | `MissouriWithholdingCalculator` |
| **MS** | `CodedFormula` | `MississippiWithholdingCalculator` |
| **MT** | `CodedFormula` | `MontanaWithholdingCalculator` |
| **NC** | `CodedFormula` | `NorthCarolinaWithholdingCalculator` |
| **ND** | `CodedFormula` | `NorthDakotaWithholdingCalculator` |
| **NE** | `CodedFormula` | `NebraskaWithholdingCalculator` |
| **NH** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **NJ** | `CodedFormula` | `NewJerseyWithholdingCalculator` |
| **NM** | `CodedFormula` | `NewMexicoWithholdingCalculator` |
| **NV** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **NY** | `CodedFormula` | `NewYorkWithholdingCalculator` |
| **OH** | `CodedFormula` | `OhioWithholdingCalculator` |
| **OK** | `JsonTable` | `OklahomaWithholdingCalculator` |
| **OR** | `CodedFormula` | `OregonWithholdingCalculator` |
| **PA** | `CodedFormula` | `PennsylvaniaWithholdingCalculator` |
| **RI** | `CodedFormula` | `RhodeIslandWithholdingCalculator` |
| **SC** | `CodedFormula` | `SouthCarolinaWithholdingCalculator` |
| **SD** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **TN** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **TX** | `NoIncomeTaxAdapter` | `NoIncomeTaxWithholdingAdapter` |
| **UT** | `CodedFormula` | `UtahWithholdingCalculator` |
| **VA** | `CodedFormula` | `VirginiaWithholdingCalculator` |
| **VT** | `CodedFormula` | `VermontWithholdingCalculator` |
| **WA** | `NoIncomeTaxAdapter` | `WashingtonWithholdingCalculator` |
| **WI** | `CodedFormula` | `WisconsinWithholdingCalculator` |
| **WV** | `CodedFormula` | `WestVirginiaWithholdingCalculator` |
| **WY** | `NoIncomeTaxAdapter` | `WyomingWithholdingCalculator` |

WA and WY carry the `NoIncomeTaxAdapter` implementation type — accurate, since neither taxes
income — while still using dedicated calculator classes.

---

## Employee-paid disability / paid-leave lines

Four jurisdictions surface a non-zero `DisabilityInsurance` amount, each with its own label and
its own `PayrollAssessment`-scoped manifest rule:

| State | Line | Manifest source |
|---|---|---|
| **CA** | State Disability Insurance (SDI) | *State Disability Insurance (SDI) Rates and Withholding* |
| **CO** | FAMLI paid family & medical leave premium | *Colorado FAMLI Premiums and Reports* |
| **CT** | Paid leave contribution | *Connecticut Paid Leave Contributions* |
| **WA** | WA Cares Fund (long-term care), 0.58% | *RCW 50B.04.080 — WA Cares Premium Assessment* |

`PayCalculator` emits the disability line **only when the amount exceeds zero**, so employees in
the other 47 jurisdictions never see an empty row.

These are wage-based payroll assessments, not income tax. They are deliberately **excluded** from
bonus mode and self-employment mode, and both modes carry an `AccuracyNote` saying so.

---

## Documented intentional quirks

Three behaviors look like bugs and are not. Each is deliberate, each has a matching test, and
each stays until replaced by a verified, tested fix.

**California — the 3-cent single-status adjustment.** `CaliforniaWithholdingCalculator` subtracts
exactly `$0.03` from Method B withholding for the Single filing status:

```csharp
// Workaround: Single filing status is off by 3 cents
withholding = Math.Max(0m, withholding - 0.03m);
```

It even appears as a step in the generated worksheet, so the user is not misled about where the
number came from.

**Oklahoma — whole-dollar rounding.** `OklahomaOw2PercentageCalculator` finishes with
`RoundToNearestWholeDollar(tax)` rather than cent rounding, because Form OW-2 specifies
whole-dollar withholding. Covered by `OklahomaOw2RoundingTest`.

**Alabama — depends on federal withholding.** `AlabamaWithholdingCalculator` uses annualized
federal withholding (from `context.FederalWithholdingPerPeriod`) plus dependent deductions when
computing state taxable income. This is the reason `PayCalculator` computes federal before state
and threads the result through the context.

If a state rule looks odd, **check the matching test before changing it** — the test usually
records the citation that explains why.

---

## Shared helpers

`Core/Tax/State/StateExplanationSteps.cs` provides the building blocks that keep 44 calculators'
narratives consistent — including `NoIncomeTax(state)` for the standard zero-tax step, plus
`Money(...)` and `Percent(...)` formatters pinned to `en-US`.

---

## Adding a new state, or updating an existing one

1. **Create or edit `Core/Data/Schemas/<state>.json`** with the fields the state's form needs.
   Label them with real form line references.
2. **Create or edit `Core/Tax/<StateName>/<StateName>WithholdingCalculator.cs`** implementing
   `IStateWithholdingCalculator`. Take `IStateSchemaProvider` in the constructor if `Validate`
   needs the option set. Supply `WithholdingSteps` and `WithholdingReference` — a state
   without a worksheet narrative is a worse experience than one with.
3. **Register it** in `AddPaycheckCalculatorCore`.
4. **Add or update the manifest rule** in `tax_source_manifest_2026.json` with the correct
   `calculatorClass`, official HTTPS URL, revision/effective date, verification date,
   `approximations`, `exclusions`, and `applicabilityNotes`. Startup validation will reject
   anything incomplete.
5. **Write `<StateName>WithholdingCalculatorTest.cs`** using explicit numeric expected values
   taken from the published table — bracket boundaries, allowance handling, extra withholding,
   pre-tax deduction effects, and rounding edges.
6. If the state needs a new tax data file, **wire it into all three asset paths** — see
   [09 — Tax Data Files](09-tax-data-files.md).

Several states also carry a `README.md` beside their calculator recording sources and
derivations; add one when the rules are non-obvious.

---

**Next:** [06 — Alternate Calculators](06-alternate-calculators.md)
