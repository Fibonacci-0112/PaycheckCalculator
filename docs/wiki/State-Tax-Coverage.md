# State Tax Coverage

PaycheckCalc supports all 50 US states plus the District of Columbia. Every jurisdiction has its
**own dedicated `IStateWithholdingCalculator`** under `PaycheckCalc.Core/Tax/<StateName>/`, registered
centrally in `StateCalculatorRegistry` via `AddPaycheckCalcCore`
(`PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`). This page describes
the calculator categories, individual state details, and how to add or change a state.

---

## State Calculator Categories

### No state income tax (9 states)

Seven states share the `NoIncomeTaxWithholdingAdapter`, which returns zero withholding:

**AK, FL, NV, NH, SD, TN, TX**

Two more states have **no income tax but a dedicated calculator** because they levy an employee-paid
payroll assessment (or simply to keep a per-state folder/schema):

| State | Calculator | Notes |
|---|---|---|
| WA | `WashingtonWithholdingCalculator` | No income tax; **WA Cares Fund** (Long-Term Care) at **0.58%** of gross wages, with an optional exemption toggle (`WaCaresExempt`). |
| WY | `WyomingWithholdingCalculator` | No income tax and no employee-paid state payroll assessment (empty input schema). |

### States with a dedicated income-tax calculator (41 states + DC)

Each of the following implements its own `IStateWithholdingCalculator` with state-specific forms,
deductions, allowances/exemptions, and graduated brackets or flat rate. Five of them are backed by
JSON tax tables (AR, CA, CO, CT, OK — see [JSON-backed calculators](#json-backed-calculators)).

| State | Calculator | Key features |
|---|---|---|
| AL | `AlabamaWithholdingCalculator` | Graduated brackets, 5 filing statuses, dependent deductions, **annualized federal-withholding deduction** |
| AZ | `ArizonaWithholdingCalculator` | Form A-4 percentage-**election** method (0.5%–3.5% of gross taxable wages; 2.0% default when no A-4 is on file) |
| AR | `ArkansasWithholdingCalculator` | Arkansas DFA formula method with transitional-zone brackets (JSON-backed) |
| CA | `CaliforniaWithholdingCalculator` | Method B (EDD DE 44 percentage tables) **+ SDI 1.3%** (JSON-backed) |
| CO | `ColoradoWithholdingCalculator` | Flat **4.4%** + DR 0004 Table 1 allowance **+ FMLI 0.044%** (JSON-backed) |
| CT | `ConnecticutWithholdingCalculator` | TPG-211 table-driven withholding (6.99% flat when no CT-W4) **+ PFMLI 0.5%** (JSON-backed) |
| DC | `DistrictOfColumbiaWithholdingCalculator` | D-4, $15,000/$30,000 std deduction, $1,675 per-allowance exemption, FR-230 graduated brackets (4%–10.75%) |
| DE | `DelawareWithholdingCalculator` | DE W-4, $110 personal credit per allowance, 7 graduated brackets (top 6.6% over $60k) |
| GA | `GeorgiaWithholdingCalculator` | Flat **5.19%** (HB 111), G-4 statuses A/B/C/D, $12,000/$24,000 std deduction, $4,000 dependent + $3,000 additional allowance |
| HI | `HawaiiWithholdingCalculator` | Booklet A percentage method, $2,200/$4,400 std deduction, $1,144 per HW-4 allowance, brackets 1.4%–11.0% |
| ID | `IdahoWithholdingCalculator` | Flat **5.3%** (HB 521), $16,100/$32,200 std deduction, $3,300 per ID W-4 allowance |
| IL | `IllinoisWithholdingCalculator` | Flat **4.95%**, IL-W-4 basic allowances ($2,925/yr) + additional allowances ($1,000/yr) |
| IN | `IndianaWithholdingCalculator` | Flat **3.05%**, WH-4 personal/age/blind exemptions ($1,000 each) + dependent exemption ($3,000 each) |
| IA | `IowaWithholdingCalculator` | Flat **3.65%**, no standard deduction/allowance |
| KS | `KansasWithholdingCalculator` | K-4, $3,605/$8,240 std deduction, $2,250 per allowance, two brackets (5.20%/5.58%) |
| KY | `KentuckyWithholdingCalculator` | Flat **4.0%**, $3,160 std deduction, $10 K-4 per-allowance credit |
| LA | `LouisianaWithholdingCalculator` | L-4, $4,500/$9,000 personal exemption, $1,000 per dependent, three brackets (1.85%/3.50%/4.25%) |
| ME | `MaineWithholdingCalculator` | W-4ME, $15,300/$30,600 std deduction, $5,300 per allowance, three brackets (5.80%/6.75%/7.15%) |
| MD | `MarylandWithholdingCalculator` | MW507, variable std deduction (15% of wages, min $1,600/$3,200, max $2,550/$5,100), $3,200 per exemption, ten brackets (2%–6.5%) |
| MA | `MassachusettsWithholdingCalculator` | M-4, personal exemptions $4,400/$8,800/$6,800, $1,000/dependent, $2,200/blind, $700/age-65+, flat 5% **+ 4% surtax over $1M** |
| MI | `MichiganWithholdingCalculator` | Flat **4.25%**, MI-W4 $5,900 per exemption |
| MN | `MinnesotaWithholdingCalculator` | W-4MN, $15,300/$30,600/$23,000 std deduction, $5,300 per allowance, four brackets (5.35%/6.80%/7.85%/9.85%) |
| MS | `MississippiWithholdingCalculator` | 89-350, $2,300/$4,600/$3,400 std deduction, $6,000/$12,000/$9,500 personal exemption, $1,500/dependent, two brackets (0% to $10k, 4% over) |
| MO | `MissouriWithholdingCalculator` | MO W-4, $15,750/$31,500/$23,625 std deduction (mirrors federal), $2,100 per allowance, eight brackets (0%–4.7%) |
| MT | `MontanaWithholdingCalculator` | MW-4, variable std deduction (20% of wages, min $4,370/$8,740, max $5,310/$10,620), $3,040 per exemption, two brackets (4.7%/5.9%) |
| NE | `NebraskaWithholdingCalculator` | W-4N, $8,600/$17,200/$12,900 std deduction, $171 per-allowance credit, four brackets (2.46%/3.51%/5.01%/5.2%) |
| NJ | `NewJerseyWithholdingCalculator` | NJ-W4 statuses A–E, $1,000 per-allowance deduction, Table A (single) and Table B (married/HoH) brackets |
| NM | `NewMexicoWithholdingCalculator` | RPD-41272, $15,750/$31,500/$23,625 std deduction, $4,000 per exemption, five brackets (1.7%–5.9%) |
| NY | `NewYorkWithholdingCalculator` | IT-2104, $8,000/$16,050/$11,000 std deduction, $1,000 per allowance, ten brackets (4%–10.9%) |
| NC | `NorthCarolinaWithholdingCalculator` | NC-4, $12,750/$25,500/$19,125 std deduction, $2,500 per allowance, flat **4.5%** |
| ND | `NorthDakotaWithholdingCalculator` | Federal W-4 statuses, $15,750/$31,500/$23,625 std deduction, three brackets (1.10%/2.04%/2.64%) |
| OH | `OhioWithholdingCalculator` | IT-4 exemption ($650 annualized per exemption), two brackets (0% up to $26,050, 2.75% over) |
| OK | `OklahomaWithholdingCalculator` | OW-2 percentage method with **whole-dollar rounding** (JSON-backed) |
| OR | `OregonWithholdingCalculator` | OR-W-4, $2,835/$5,670 std deduction, $219 per-allowance **credit**, four brackets (4.75%/6.75%/8.75%/9.9%) |
| PA | `PennsylvaniaWithholdingCalculator` | Flat **3.07%** |
| RI | `RhodeIslandWithholdingCalculator` | RI W-4, $10,550 std deduction, $4,700 per exemption, three brackets (3.75%/4.75%/5.99%) |
| SC | `SouthCarolinaWithholdingCalculator` | SC W-4, variable std deduction (10% of wages, max $7,500), $5,000 per allowance, three brackets (0%/3%/6%) |
| UT | `UtahWithholdingCalculator` | Flat **4.5%** with a phase-out allowance credit |
| VT | `VermontWithholdingCalculator` | W-4VT, no std deduction, $5,400 per allowance, four brackets (3.35%/6.60%/7.60%/8.75%) |
| VA | `VirginiaWithholdingCalculator` | VA-4, $8,750/$17,500 std deduction, $930 per exemption, four brackets (2%/3%/5%/5.75%) |
| WV | `WestVirginiaWithholdingCalculator` | IT-104, no std deduction, $2,000 per exemption, five brackets (3%–6.5%) |
| WI | `WisconsinWithholdingCalculator` | WT-4, $12,760/$23,170/$16,840 std deduction, $700 per allowance, four brackets (3.54%/4.65%/5.30%/7.65%) |

> The tax amounts, brackets, deductions, and rates above are the 2026 values encoded in each calculator
> and its matching `*Test.cs` file. When the implementation and these notes disagree, **trust the
> implementation and tests** and repair the docs.

### A note on the legacy generic adapter

`PercentageMethodWithholdingAdapter` (driven by `StateTaxConfigs2026`) was the original
configuration-driven approach for annualized graduated-bracket states. Every state now has a dedicated
calculator, so **`StateTaxConfigs2026.Configs` is empty** and the adapter is **not wired to any production
state**. It is retained only for tests and as a reference implementation.

---

## State Disability / Paid-Leave Insurance

Some states levy an additional employee-paid payroll premium beyond income tax. These flow through
`StateWithholdingResult.DisabilityInsurance` (with a dynamic label) and appear as a separate line item on
the results screen, chart, and exports:

| State | Premium | Rate | Label on results |
|---|---|---|---|
| CA | State Disability Insurance (SDI) | **1.3%** of gross wages | "State Disability Insurance (SDI)" |
| CO | Family & Medical Leave Insurance (FMLI) | **0.044%** of gross wages | "State Disability Insurance" |
| CT | Paid Family & Medical Leave Insurance (PFMLI) | **0.5%** of gross wages | "Family Leave Insurance (FLI)" |
| WA | WA Cares Fund (Long-Term Care) | **0.58%** of gross wages | "WA Cares Fund (Long-Term Care)" |

---

## Plugin Architecture

### IStateWithholdingCalculator interface

Every state calculator implements:

```
UsState State                                          → which state this calculator handles
GetInputSchema()  → IReadOnlyList<StateFieldDefinition>  → what input fields the state needs
Validate(values)  → IEnumerable<string>                  → validates user-supplied values
Calculate(context, values) → StateWithholdingResult      → computes withholding for one pay period
```

`Calculate` receives a `CommonWithholdingContext` (state, gross wages, pay frequency, tax year, pre-tax
deductions that reduce state taxable wages, and federal withholding per period) and returns taxable
wages, state income-tax withholding, any disability/leave premium, and its display label — plus
"Show Your Work" explanation steps.

### StateCalculatorRegistry

The registry maps each `UsState` enum value to its `IStateWithholdingCalculator`. It is built at startup
in `AddPaycheckCalcCore` and injected into `PayCalculator`. `SupportedStates` drives the state picker in
both front-ends.

### Schema-driven dynamic inputs

State input fields are declared as `StateFieldDefinition` objects (key, label, field type, picker
options, default value, required flag) returned from `GetInputSchema()` — backed by
`JsonStateSchemaProvider` over `PaycheckCalc.Core/Data/Schemas/*.json` (one file per state). Both UIs read
the schema and render fields dynamically (MAUI via `StateFieldViewModel`); user values are collected into
`StateInputValues` and passed to the calculator. **Do not hard-code per-state controls in the UI** when
the schema can express them.

### JSON-backed calculators

Five state calculators load 2026 tax tables from JSON at startup (alongside the federal IRS 15-T table):

| Calculator | Data file |
|---|---|
| `ArkansasFormulaCalculator` (wrapped by `ArkansasWithholdingCalculator`) | `ar_withholding_2026.json` |
| `CaliforniaPercentageCalculator` (wrapped by `CaliforniaWithholdingCalculator`) | `ca_method_b_2026.json` |
| `ColoradoWithholdingCalculator` | `co_dr0004_2026.json` |
| `ConnecticutWithholdingCalculator` | `connecticut_withholding_2026.json` |
| `OklahomaOw2PercentageCalculator` (wrapped by `OklahomaWithholdingCalculator`) | `ok_ow2_2026_percentage.json` |

---

## Adding or Changing a State

Because every state already has a dedicated calculator, new work means either editing an existing
calculator or adding a brand-new jurisdiction with the custom-calculator pattern:

1. Create a folder: `PaycheckCalc.Core/Tax/<StateName>/`.
2. Implement `IStateWithholdingCalculator` with `GetInputSchema()`, `Validate()`, and `Calculate()`.
3. Add the state's schema JSON to `PaycheckCalc.Core/Data/Schemas/<abbr>.json`.
4. If the calculator needs tax-table JSON, add it to `PaycheckCalc.Core/Data/`, load it in
   `AddPaycheckCalcCore`, and register the file as an asset in **all** consumers: `PaycheckCalc.App`
   (`MauiAsset`), `PaycheckCalc.Blazor` (linked into the `TaxData/` build output), and
   `PaycheckCalc.Tests` (linked `None` copied to output).
5. Register the calculator in `AddPaycheckCalcCore` within the `StateCalculatorRegistry` setup.
6. Add regression tests in `PaycheckCalc.Tests/<StateName>WithholdingCalculatorTest.cs`.

When you add or change state inputs, keep all four in sync: **schema, validation, UI field resolution,
and tests.**

### Testing a state

- Add or update `PaycheckCalc.Tests/<StateName>WithholdingCalculatorTest.cs`.
- Cover filing statuses, allowance/exemption handling, bracket boundaries, extra withholding, pre-tax
  deduction effects, rounding edges, and any state-specific quirks (e.g. Alabama's federal deduction,
  California's 3-cent single-status adjustment, Oklahoma's whole-dollar rounding).
- Use **explicit expected dollar amounts** taken from the rule/table — do not recompute with production
  helpers.
