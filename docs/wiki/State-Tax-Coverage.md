# State Tax Coverage

PaycheckCalc supports all 50 US states plus the District of Columbia. This page describes the calculator categories, individual state details, and how to add a new state.

---

## State Calculator Categories

### No Income Tax (8 states)

These states have no individual income tax and are implemented through `NoIncomeTaxWithholdingAdapter`, which returns zero withholding.

**AK, FL, NV, NH, SD, TN, TX, WY**

### Flat Rate (1 state)

| State | Rate | Calculator |
|---|---|---|
| PA | 3.07% | `PennsylvaniaWithholdingCalculator` |

### Custom Formula States (12 states)

These states have unique calculation rules that require dedicated calculator implementations.

| State | Calculator | Key Features |
|---|---|---|
| AL | `AlabamaWithholdingCalculator` | Graduated brackets, 5 filing statuses, dependent deductions, federal withholding deduction |
| AR | `ArkansasWithholdingCalculator` | DFA formula method with transitional zone brackets (JSON-backed) |
| CA | `CaliforniaWithholdingCalculator` | Method B (EDD DE 44), State Disability Insurance (SDI) at 1.2% (JSON-backed) |
| CO | `ColoradoWithholdingCalculator` | Flat 4.4% with DR 0004 Table 1 allowance + Family & Medical Leave Insurance (FMLI) (JSON-backed) |
| CT | `ConnecticutWithholdingCalculator` | TPG-211 table-driven withholding + Paid Family & Medical Leave Insurance (PFMLI) (JSON-backed) |
| DC | `DistrictOfColumbiaWithholdingCalculator` | D-4 with 4 filing statuses, $1,675 per-allowance exemption, 7 graduated brackets (top rate 10.75% over $1M) |
| DE | `DelawareWithholdingCalculator` | DE W-4 with 4 filing statuses, $110 personal credit per allowance, 7 graduated brackets (top rate 6.6% over $60k) |
| GA | `GeorgiaWithholdingCalculator` | Flat 5.19% (HB 111), Form G-4 filing statuses A/B/C/D, $12,000/$24,000 standard deduction, $4,000 dependent and $3,000 additional allowances |
| IL | `IllinoisWithholdingCalculator` | Flat 4.95% with IL-W-4 basic allowances ($2,925/yr each) and additional allowances ($1,000/yr each) |
| ND | `NorthDakotaWithholdingCalculator` | Federal W-4 filing statuses (Single/Married/HoH), $15,750/$31,500/$23,625 std deduction (mirrors federal), three brackets (1.10%/2.04%/2.64%) |
| OK | `OklahomaWithholdingCalculator` | OW-2 percentage method with whole-dollar rounding (JSON-backed) |
| PA | `PennsylvaniaWithholdingCalculator` | Flat 3.07% |
| WA | `WashingtonWithholdingCalculator` | No income tax; WA Cares Fund (Long-Term Care) at 0.58% of gross wages, with optional exemption toggle |

### Annualized Percentage Method States (30 states)

These states use `PercentageMethodWithholdingAdapter` with state-specific configurations defined in `StateTaxConfigs2026`:

**AZ, HI, IA, ID, IN, KS, KY, LA, MA, MD, ME, MI, MN, MO, MS, MT, NC, NE, NJ, NM, NY, OH, OR, RI, SC, UT, VA, VT, WI, WV**

Each configuration specifies:
- Standard deduction amounts (per filing status)
- Personal allowance value
- Graduated tax brackets (per filing status)

The adapter provides a standard input schema: Filing Status (Single/Married), State Allowances, and Extra Withholding.

---

## State Disability / Family Leave Insurance

Some states levy additional payroll taxes beyond income tax:

| State | Tax | Rate | Label on Results |
|---|---|---|---|
| CA | State Disability Insurance (SDI) | 1.2% | "State Disability Insurance (SDI)" |
| CO | Family & Medical Leave Insurance (FMLI) | 0.45% | "State Disability Insurance" |
| CT | Paid Family & Medical Leave Insurance (PFMLI) | 0.5% | "Family Leave Insurance (FLI)" |
| WA | WA Cares Fund (Long-Term Care Insurance) | 0.58% | "WA Cares Fund (Long-Term Care)" |

These amounts flow through `StateWithholdingResult.DisabilityInsurance` and appear as separate line items on the results screen and chart.

---

## Local (Sub-State) Tax Coverage

PaycheckCalc models local / sub-state payroll taxes behind an `ILocalWithholdingCalculator` plugin model, with calculators registered in a `LocalCalculatorRegistry` keyed by locality code. `PayCalculator` consumes the local registry after state withholding.

| Jurisdiction | Calculator | JSON Data |
|---|---|---|
| Pennsylvania Act 32 EIT | `PaEitCalculator` | `pa_eit_2026.json` |
| Pennsylvania LST | `PaLstCalculator` | (flat head tax, no table) |
| New York City | `NycWithholdingCalculator` | `nyc_withholding_2026.json` |
| Ohio (RITA) | `OhRitaCalculator` | `oh_rita_2026.json` |
| Ohio (CCA) | `OhCcaCalculator` | `oh_cca_2026.json` |
| Maryland county surtax | `MdCountyCalculator` | `md_county_surtax_2026.json` |

Local taxes are **additive**: they subtract from net pay but do **not** reduce federal or state taxable wages. `PaycheckResult` exposes `LocalWithholding`, `LocalHeadTax` (e.g., PA LST), `LocalityLabel`, `LocalTaxableWages`, and `LocalBreakdown` for itemized display.

---

## Plugin Architecture

### IStateWithholdingCalculator Interface

Every state calculator implements three methods:

```
GetInputSchema()  → Declares what input fields the state needs (filing status, allowances, etc.)
Validate(values)  → Validates user-supplied values against state rules
Calculate(context, values)  → Computes state withholding for one pay period
```

### StateCalculatorRegistry

The registry maps each `UsState` enum value to its `IStateWithholdingCalculator`. It is built at startup in `AddPaycheckCalcCore` (`PaycheckCalc.Core/DependencyInjection/PaycheckCoreServiceCollectionExtensions.cs`) and injected into `PayCalculator`.

### Dynamic State Inputs

State input fields are defined as `StateFieldDefinition` objects with:
- `Key` — unique identifier (e.g., `"FilingStatus"`, `"Allowances"`)
- `Label` — display text
- `FieldType` — `Picker`, `Integer`, `Decimal`, `Boolean`
- `Options` — picker choices (when applicable)
- `DefaultValue` — initial value
- `IsRequired` — validation flag

The UI reads the schema via `GetInputSchema()` and renders `StateFieldViewModel` instances dynamically. User values are collected into `StateInputValues` (a dictionary wrapper) and passed to the calculator.

---

## Adding a New State

### Option A: Generic Percentage Method

If the state uses a standard annualized graduated bracket approach:

1. Add an entry to `StateTaxConfigs2026.Configs` in `PaycheckCalc.Core/Tax/State/StateTaxConfigs2026.cs`.
2. Define the standard deduction, allowance value, and bracket tables for Single and Married filing statuses.
3. The `PercentageMethodWithholdingAdapter` will automatically provide the standard schema (Filing Status, Allowances, Extra Withholding).

### Option B: Custom Calculator

If the state has unique inputs, formulas, or additional taxes:

1. Create a new folder: `PaycheckCalc.Core/Tax/<StateName>/`.
2. Implement `IStateWithholdingCalculator` with custom `GetInputSchema()`, `Validate()`, and `Calculate()`.
3. If the calculator needs JSON data, add the file to `PaycheckCalc.Core/Data/` and register it as an asset in `PaycheckCalc.App` (as a `MauiAsset`) and `PaycheckCalc.Tests` (as a linked `None` item copied to the output directory).
4. Register the calculator in `AddPaycheckCalcCore` (`PaycheckCoreServiceCollectionExtensions.cs`) within the `StateCalculatorRegistry` setup.
5. Add regression tests in `PaycheckCalc.Tests/`.

### Testing a New State

- Add a test file: `PaycheckCalc.Tests/<StateName>WithholdingCalculatorTest.cs`.
- Cover filing statuses, allowance handling, bracket boundaries, extra withholding, pre-tax deduction effects, and any state-specific quirks.
- Use explicit expected dollar amounts rather than recomputing with production logic.
