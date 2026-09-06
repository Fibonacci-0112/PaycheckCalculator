# State Tax Coverage

PaycheckCalculator supports all 50 US states plus the District of Columbia.

Every jurisdiction is represented by an `IStateWithholdingCalculator` registered in `StateCalculatorRegistry` during `AddPaycheckCalculatorCore`. Dynamic state-specific UI fields are defined by schema JSON files in `PaycheckCalculator.Core/Data/Schemas/`.

---

## Calculator Categories

### Shared no-income-tax adapter

The following states have no state income tax and share `NoIncomeTaxWithholdingAdapter`:

**AK, FL, NV, NH, SD, TN, TX**

### Dedicated no-income-tax calculators

| State | Calculator | Notes |
|---|---|---|
| WA | `WashingtonWithholdingCalculator` | No income tax; computes WA Cares Fund when the employee is not exempt. |
| WY | `WyomingWithholdingCalculator` | No income tax and no employee-paid state payroll assessment. |

### Dedicated income-tax calculators

Every income-tax jurisdiction has a dedicated calculator under `PaycheckCalculator.Core/Tax/<StateName>/`.

| State | Calculator | Notes |
|---|---|---|
| AL | `AlabamaWithholdingCalculator` | Graduated brackets, multiple statuses, dependent deductions, federal-withholding deduction. |
| AZ | `ArizonaWithholdingCalculator` | A-4 percentage-election method. |
| AR | `ArkansasWithholdingCalculator` | JSON-backed DFA formula method. |
| CA | `CaliforniaWithholdingCalculator` | Method B, JSON-backed percentage data, SDI. |
| CO | `ColoradoWithholdingCalculator` | JSON-backed DR 0004 allowance data, flat income tax, FAMLI. |
| CT | `ConnecticutWithholdingCalculator` | JSON-backed TPG-211-style withholding, PFMLI. |
| DC | `DistrictOfColumbiaWithholdingCalculator` | D-4-style inputs and graduated brackets. |
| DE | `DelawareWithholdingCalculator` | DE W-4 allowances and graduated brackets. |
| GA | `GeorgiaWithholdingCalculator` | G-4 inputs, standard deduction, allowances/dependents. |
| HI | `HawaiiWithholdingCalculator` | HW-4 allowances, graduated brackets, TDI. |
| IA | `IowaWithholdingCalculator` | Flat-rate withholding model. |
| ID | `IdahoWithholdingCalculator` | Flat-rate model with state-specific deduction/allowance handling. |
| IL | `IllinoisWithholdingCalculator` | Flat rate with IL-W-4 allowances. |
| IN | `IndianaWithholdingCalculator` | Flat rate with WH-4 exemption inputs. |
| KS | `KansasWithholdingCalculator` | K-4 allowances and bracket formula. |
| KY | `KentuckyWithholdingCalculator` | Flat rate with standard deduction / allowance credit behavior. |
| LA | `LouisianaWithholdingCalculator` | L-4 exemptions/dependents and brackets. |
| MA | `MassachusettsWithholdingCalculator` | M-4 exemptions, surtax handling, PFML. |
| MD | `MarylandWithholdingCalculator` | MW507 exemptions and the guide's per-payroll-period percentage method (flat $3,400 standard deduction, 4.75% minimum rate), plus county income tax. |
| ME | `MaineWithholdingCalculator` | W-4ME allowances and brackets. |
| MI | `MichiganWithholdingCalculator` | Flat rate with MI-W4 exemptions. |
| MN | `MinnesotaWithholdingCalculator` | W-4MN allowances and brackets. |
| MO | `MissouriWithholdingCalculator` | MO W-4 allowances and brackets. |
| MS | `MississippiWithholdingCalculator` | State-specific exemptions and brackets. |
| MT | `MontanaWithholdingCalculator` | MW-4 exemptions and brackets. |
| NC | `NorthCarolinaWithholdingCalculator` | Flat rate with NC-4 allowance handling. |
| ND | `NorthDakotaWithholdingCalculator` | Federal-style statuses and brackets. |
| NE | `NebraskaWithholdingCalculator` | W-4N credits and brackets. |
| NJ | `NewJerseyWithholdingCalculator` | NJ-W4 status tables, TDI and FLI. |
| NM | `NewMexicoWithholdingCalculator` | RPD-41272-style deductions/exemptions and brackets. |
| NY | `NewYorkWithholdingCalculator` | IT-2104 allowances and brackets, DBL and PFL. |
| OH | `OhioWithholdingCalculator` | IT-4 exemptions and two-bracket formula. |
| OK | `OklahomaWithholdingCalculator` | JSON-backed OW-2 percentage method with whole-dollar rounding. |
| OR | `OregonWithholdingCalculator` | OR-W-4 allowance credit and brackets, Paid Leave Oregon. |
| PA | `PennsylvaniaWithholdingCalculator` | Flat income tax. |
| RI | `RhodeIslandWithholdingCalculator` | RI W-4 exemptions and brackets, TDI. |
| SC | `SouthCarolinaWithholdingCalculator` | SC W-4 allowances and brackets. |
| UT | `UtahWithholdingCalculator` | Flat rate with phase-out allowance credit. |
| VA | `VirginiaWithholdingCalculator` | VA-4 exemptions and brackets. |
| VT | `VermontWithholdingCalculator` | W-4VT allowances and brackets. |
| WI | `WisconsinWithholdingCalculator` | WT-4 allowances and brackets. |
| WV | `WestVirginiaWithholdingCalculator` | IT-104 exemptions and brackets. |

When documentation and implementation disagree, trust the calculator and its tests first, then update the docs.

---

## State Tax Lines

Every state result carries an ordered `StateTaxLine` list — state, county and local income tax plus one line per employee-paid payroll assessment. `StateTaxLineOrdering` defines the order once (income broadest to narrowest, then assessments largest first) and both front-ends, all four exporters, and the A/B comparison use it.

The scalar `StateWithholding` and `StateDisabilityInsurance` members are **derived** from those lines, so they cannot disagree with what the user sees.

### Employee-paid payroll assessments

Rates, wage bases and caps live in `PaycheckCalculator.Core/Data/state_payroll_assessments_2026.json`, each entry citing the official 2026 publication behind it.

| Jurisdiction | Programs | Rate | Cap |
|---|---|---|---|
| CA | State Disability Insurance (SDI) | 1.30% | none |
| CO | Family and Medical Leave Insurance (FAMLI) | 0.44% | $184,500 wage base |
| CT | Paid Family and Medical Leave (PFMLI) | 0.50% | cap disclosed, not applied |
| HI | Temporary Disability Insurance (TDI) | 0.50% | $7.50 per week |
| MA | Paid Medical Leave / Paid Family Leave | 0.28% / 0.18% | $184,500 wage base |
| NJ | Temporary Disability Insurance / Family Leave Insurance | 0.19% / 0.23% | $171,100 wage base |
| NY | Disability Benefits (DBL) / Paid Family Leave (PFL) | 0.50% / 0.432% | $0.60 per week / $411.91 per year |
| OR | Paid Leave Oregon | 0.60% | $184,500 wage base |
| RI | Temporary Disability Insurance (TDI) | 1.10% | $100,000 wage base |
| WA | WA Cares Fund (Long-Term Care) | 0.58% | none |

Three cap shapes are modeled explicitly rather than approximated: an annual taxable wage base, a statutory per-week ceiling that scales with payroll frequency, and a maximum contribution for the year. `PaycheckInput.YtdStateWages` supplies the year-to-date figure the capped programs need.

Programs that exist but are **not** withheld — Washington's PFML, Minnesota Paid Leave, Maine PFML, Delaware Paid Leave — are disclosed as exclusions on the state's regular-withholding rule so the note reaches the user.

## Local Income Tax

| Jurisdiction | Coverage |
|---|---|
| MD | County income tax for all 23 counties plus Baltimore City, from `Data/md_county_rates_2026.json`. Anne Arundel and Frederick apply graduated marginal rates. Selected via a schema-driven `County` picker; defaults to the highest local rate when unreported, as the Comptroller directs. |

No other local income tax is withheld. Where a state has one — Indiana counties, NYC/Yonkers, PA EIT, Ohio municipalities, Michigan cities, Missouri earnings taxes, Kentucky occupational taxes — the state's manifest rule discloses that it is not applied.

---

## Plugin Architecture

### `IStateWithholdingCalculator`

Each calculator implements:

```text
UsState State
IReadOnlyList<StateFieldDefinition> GetInputSchema()
IEnumerable<string> Validate(StateInputValues values)
StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
```

The calculator receives common paycheck context and the state-specific values collected by the UI. It returns taxable wages, withholding, optional disability / paid-leave premium, labels, and explanation steps.

### `StateCalculatorRegistry`

The registry maps `UsState` to the corresponding calculator. `PayCalculator` asks the registry for the selected state's calculator and delegates state withholding to it.

`SupportedStates` drives the state picker in both front-ends.

### Dynamic state schemas

State field definitions come from `PaycheckCalculator.Core/Data/Schemas/*.json` through `JsonStateSchemaProvider`. Both front-ends use these schemas to render state-specific fields.

Do not hard-code a state-specific control in the UI when the schema can express the field.

---

## JSON-Backed Calculators

The following calculators load tax-table data from JSON at startup:

| Calculator | Data file |
|---|---|
| `Irs15TPercentageCalculator` | `us_irs_15t_2026_percentage_automated.json` |
| `ArkansasFormulaCalculator` | `ar_withholding_2026.json` |
| `CaliforniaPercentageCalculator` | `ca_method_b_2026.json` |
| `ColoradoWithholdingCalculator` | `co_dr0004_2026.json` |
| `ConnecticutWithholdingCalculator` | `connecticut_withholding_2026.json` |
| `OklahomaOw2PercentageCalculator` | `ok_ow2_2026_percentage.json` |
| `StateSupplementalCalculator` | `state_supplemental_2026.json` |
| `StatePayrollAssessments` | `state_payroll_assessments_2026.json` |
| `MarylandCountyRates` | `md_county_rates_2026.json` |

If a tax-data file is renamed, update `AddPaycheckCalculatorCore`, MAUI `MauiAsset` entries, Blazor `TaxData` links, test project copy/link entries, and any tests or docs that reference the file.

`tax_source_manifest_2026.json` separately maps every state/DC regular-withholding calculator, supplemental method, and applicable payroll assessment to official publications. `TaxSourceCatalog` validates complete regular-withholding coverage and calculator-class correspondence during startup. Update the manifest whenever a state rule or calculator mapping changes, following [Accuracy and Source Governance](Accuracy-and-Source-Governance.md).

---

## Legacy Generic Adapter

`PercentageMethodWithholdingAdapter` and `StateTaxConfigs2026` remain in the codebase as reference/test infrastructure. Production state registration currently uses dedicated calculators plus the no-income-tax adapter for the plain no-tax states.

Do not add new production state logic to the generic path unless the architecture is intentionally changed and tests/docs are updated together.

---

## Adding or Changing a State

For new or changed state withholding logic:

1. Create or update `PaycheckCalculator.Core/Tax/<StateName>/`.
2. Implement or update the `IStateWithholdingCalculator`.
3. Add or update the matching schema file in `PaycheckCalculator.Core/Data/Schemas/<state>.json`.
4. Add or update tax-table JSON in `PaycheckCalculator.Core/Data/` if the calculator is table-driven.
5. Register the calculator in `AddPaycheckCalculatorCore`.
6. Update MAUI, Blazor, and test project asset/link entries if a new JSON data file is introduced.
7. Add or update xUnit tests with explicit expected dollar amounts.
8. Verify the state picker and dynamic fields render correctly in both front-ends.

Keep schema, validation, UI field resolution, calculation logic, and tests synchronized.

---

## Testing a State

State tests should cover:

- Filing statuses.
- Allowances, exemptions, dependents, and credits.
- Bracket boundaries.
- Extra withholding.
- Pre-tax deduction effects.
- State-specific rounding.
- State-specific quirks.
- Disability / paid-leave premiums where applicable.

Expected amounts should be explicit values from the applicable rule or table, not recomputed with production helpers.
