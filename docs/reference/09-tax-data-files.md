# 09 — Tax Data Files

`PaycheckCalculator.Core/Data/` holds every JSON tax table the engine loads at startup, plus the
51-file `Schemas/` directory covered in
[05 — The State Withholding Engine](05-state-withholding-engine.md#schema-driven-state-ui). This
chapter covers the tables themselves and — the part that actually bites people — the three-way
asset wiring that must move in lockstep whenever a file is added, renamed, or removed.

All files are tax-year-2026 authoritative structured data. Key names and shapes are part of the
contract with their C# loaders; don't rename or reshape a file without updating the loader and
its tests in the same change.

---

## The eight loaded files

| File | Loaded by | Shape |
|---|---|---|
| `us_irs_15t_2026_percentage_automated.json` | `Irs15TPercentageCalculator` | `year`, `annualTables.{standard, step2_checked}.{married_filing_jointly, single_or_mfs, head_of_household}[]` brackets, `worksheetConstants.line1g.{mfj, other}` |
| `ar_withholding_2026.json` | `ArkansasFormulaCalculator` | `standardDeduction`, `personalTaxCreditPerExemption`, `roundToNearest50Threshold`, `brackets[]` (`from`/`to`/`rate`/`subtraction`) |
| `ca_method_b_2026.json` | `CaliforniaPercentageCalculator` | `enumKeys` (payroll periods, threshold-status keys, rate-table-status keys), `lowIncomeExemptionThresholds`, `estimatedDeductionAllowances`, `standardDeductions`, `exemptionAllowanceCredits`, `taxRateTables`, `sourceTableMap` |
| `co_dr0004_2026.json` | `ColoradoWithholdingCalculator` | `allowances[]` keyed by filing status × number of jobs (DR 0004 Table 1) |
| `connecticut_withholding_2026.json` | `ConnecticutWithholdingCalculator` | `state`, `tax_year`, `effective_date`, `source_documents`, `notes[]`, `withholding_code_reference`, `tables` (table-driven, per TPG-211) |
| `ok_ow2_2026_percentage.json` | `OklahomaOw2PercentageCalculator` | `rounding` (whole-dollar rule), `allowanceAmounts` per frequency, `tables[]` (per-frequency single/married brackets) |
| `state_supplemental_2026.json` | `StateSupplementalCalculator` | `taxYear`, `source`, `states.<XX>.{method, rate?, note?}` |
| `tax_source_manifest_2026.json` | `TaxSourceCatalog` | `taxYear`, `rules[]` — see [07](07-explanations-and-source-governance.md) |

Every file's `source`/`source_documents`/`documentTitle` block records the publication name, its
official URL, and (where applicable) its revision — the same discipline the manifest enforces
formally, restated inline in the data itself.

### Two representative shapes

**`ok_ow2_2026_percentage.json`** — whole-dollar rounding declared as data, and per-frequency
allowance amounts so annualization does not have to guess:

```json
{
  "rounding": { "type": "nearestWholeDollar", "rule": "dropUnder50Cents_raise50to99" },
  "allowanceAmounts": { "weekly": 19.23, "biweekly": 38.46, "monthly": 83.33, ... },
  "tables": [
    { "frequency": "weekly", "single": [
      { "over": 0, "under": 194, "base": 0.0, "rate": 0.0, "excessOver": 0 },
      ...
    ]}
  ]
}
```

**`state_supplemental_2026.json`** — every state resolves to exactly one
`StateSupplementalMethod`, with an optional human-readable `note` for the messy cases:

```json
"CA": {
  "method": "FlatRate",
  "rate": 0.1023,
  "note": "California withholds bonuses and stock options at 10.23%. Other supplemental wages use 6.6%. State Disability Insurance (SDI) is withheld separately and is not included here."
}
```

The engine implements the simpler of California's two supplemental rates (the 10.23% figure) and
discloses the nuance in the note rather than silently picking the "wrong" one for every scenario.

---

## `Schemas/*.json` — the 51 state UI schema files

One file per jurisdiction, `Core/Data/Schemas/<lowercase-state>.json`, loaded by
`JsonStateSchemaProvider` and served through `IStateSchemaProvider`. Shape and behavior are
covered in
[05 — The State Withholding Engine](05-state-withholding-engine.md#schema-driven-state-ui).
A missing schema file is tolerated (empty schema); a malformed one is not.

---

## The orphan file

`Core/Data/ca_2026_method_b_calculator_ready.json` (47 KB) exists on disk but is **not
referenced by any `.csproj`, loader, or test** — grep for its filename across the repository turns
up nothing. `CaliforniaPercentageCalculator` loads `ca_method_b_2026.json` exclusively. Treat the
`_calculator_ready` file as inert unless you are specifically investigating or cleaning up stale
data artifacts; it is not part of the running system and this reference intentionally does not
document its internal shape.

---

## `ITaxDataReader` — the loading abstraction

```csharp
public interface ITaxDataReader
{
    // Returns the raw JSON for a logical name like "ar_withholding_2026.json"
    // or "schemas/ca.json". Should throw FileNotFoundException when absent so
    // the per-state schema loop can swallow missing files.
    string ReadAllText(string logicalName);
}
```

A **logical name**, not a file-system path — the same string means something different to each
host: an embedded resource identifier to MAUI, a relative disk path to Blazor, a relative test
content path to the test project. The contract to honor when implementing this interface is the
`FileNotFoundException` requirement, since `AddPaycheckCalculatorCore`'s state-schema loop
specifically catches that exception type to treat a missing schema as "no extra fields" rather
than a startup failure.

---

## The three-way asset-wiring problem

**Every tax data file must be copied into the output of every project that needs it, using that
project's own mechanism.** There is no single manifest of "which files exist" — each `.csproj`
enumerates them independently, and a rename means touching all of the following in the same
change:

### 1. `PaycheckCalculator.Core.csproj` — the canonical copy

```xml
<None Include="Data\ok_ow2_2026_percentage.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
<!-- ...one entry per top-level data file... -->
<None Include="Data\Schemas\*.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

### 2. `PaycheckCalculator.Blazor.csproj` — linked into `TaxData/`

```xml
<None Include="..\PaycheckCalculator.Core\Data\ok_ow2_2026_percentage.json">
  <Link>TaxData\ok_ow2_2026_percentage.json</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<!-- ... -->
<None Include="..\PaycheckCalculator.Core\Data\Schemas\*.json">
  <Link>TaxData\schemas\%(Filename)%(Extension)</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

`FileSystemTaxDataReader` reads from `AppContext.BaseDirectory/TaxData/<logicalName>`, so the
`Link` path (`TaxData\...`, `TaxData\schemas\...`) must exactly match the logical names
`AddPaycheckCalculatorCore` passes to `ReadAllText`.

### 3. `PaycheckCalculator.App.csproj` — packaged as `MauiAsset`

```xml
<MauiAsset Include="..\PaycheckCalculator.Core\Data\ok_ow2_2026_percentage.json" LogicalName="ok_ow2_2026_percentage.json" />
<!-- ... -->
<MauiAsset Include="..\PaycheckCalculator.Core\Data\Schemas\*.json" LogicalName="schemas\%(Filename)%(Extension)" />
```

`MauiAppPackageTaxDataReader` calls `FileSystem.OpenAppPackageFileAsync(logicalName)`, which
resolves against the `LogicalName` attribute — again, must match exactly.

### 4. `PaycheckCalculator.Tests.csproj` — content-linked for the test binary

```xml
<None Include="..\PaycheckCalculator.Core\Data\ok_ow2_2026_percentage.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  <Link>ok_ow2_2026_percentage.json</Link>
</None>
<!-- ... -->
<None Include="..\PaycheckCalculator.Core\Data\Schemas\*.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  <Link>Schemas\%(Filename)%(Extension)</Link>
</None>
```

Tests read these directly from `AppContext.BaseDirectory` (see `TestSchemas.cs` for the schema
case) rather than through `ITaxDataReader`, so most calculator unit tests construct their
calculator straight from a file, bypassing DI entirely.

### The consequence of missing a spot

If you rename `ok_ow2_2026_percentage.json` and update only `Core.csproj`:

- **Core itself still builds** — the rename is internally consistent there.
- **Blazor throws `FileNotFoundException` at runtime**, inside `AddPaycheckCalculatorCore`, the
  first time the app starts (not at compile time).
- **MAUI throws the same, on-device**, discoverable only by actually running the app.
- **Tests referencing the old filename either fail to load or silently test stale data**,
  depending on exactly what changed.

None of these are compile errors. This is precisely why `CLAUDE.md` calls out: *"If you rename a
JSON file, update every linker entry (Tests, Blazor, App), the loader in
`AddPaycheckCalculatorCore`, and the tests that reference it."* Treat a rename as a five-file
change, verified by actually running `dotnet test PaycheckCalculator.Tests` and, ideally, the
Blazor app.

---

## Adding a brand-new data file

1. Drop it under `Core/Data/`.
2. Add the `<None Include>` entry to `Core.csproj`.
3. Add the matching linked entry to `Blazor.csproj` (`TaxData\<name>`).
4. Add the matching `MauiAsset` entry to `App.csproj` (`LogicalName="<name>"`).
5. Add the matching linked entry to `Tests.csproj`.
6. Load it in `AddPaycheckCalculatorCore` via `dataReader.ReadAllText("<name>")`.
7. Write or extend the corresponding calculator and its test.

Schema files (`Schemas/*.json`) are the one category that needs no per-file edit anywhere — all
four projects already glob `Schemas\*.json` / `Data\Schemas\*.json`, so a new state's schema file
just needs to exist at the right path with the right name (`<lowercase-state-code>.json`).

---

**Next:** [10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md)
