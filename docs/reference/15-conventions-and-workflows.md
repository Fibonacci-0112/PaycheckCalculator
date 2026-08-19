# 15 — Conventions & Workflows

The rules that keep this codebase coherent across six projects, 44 state calculators, two UI
front-ends, and two AI-agent instruction formats. Most of this chapter distills what
`CLAUDE.md`, `AGENTS.md`, six per-project `AGENTS.md` files, seven `.github/instructions/*.md`
files, and `.github/copilot-instructions.md` all say — independently, and in close agreement with
each other.

---

## The layering rules, restated as a checklist

From [01](01-solution-and-build.md#dependency-graph-and-layering-rules), the invariant every
change should preserve:

- [ ] Core references no MAUI, Blazor, ASP.NET Core hosting, EF Core, HTTP client, or platform
      storage API.
- [ ] Shared references Core only — never App, Blazor, or Api.
- [ ] Api references Shared only — never App or Blazor.
- [ ] The front-ends never reference Api directly — only through `PaycheckApiClient`.
- [ ] No tax, deduction, budget, or merge logic lives in XAML code-behind, Razor components,
      converters, drawables, or API endpoint handlers. All calculation logic lives in Core; all
      merge logic lives in Shared, once.
- [ ] `StateCalculatorRegistry` registration happens **only** inside `AddPaycheckCalculatorCore` —
      never in a page, view model, or component.

A useful gut check: if you find yourself writing `if (state == UsState.CA)` anywhere outside
`Core/Tax/`, that's very likely the wrong layer.

---

## `decimal`, always

Every project's instructions repeat this independently, because it is the single easiest
correctness rule to violate by accident:

> All money, wages, rates, thresholds, and deduction values must use `decimal`. Never introduce
> `double` or `float` into calculation paths.

Binary floating point cannot represent currency exactly; `decimal` can. This is not a style
preference — a `double`-based tax bracket calculation will produce numbers that are *wrong*, not
merely imprecise, at the cent level that payroll law is specified in.

Rounding is equally disciplined: money rounds via `Math.Round(v, 2, MidpointRounding.AwayFromZero)`
everywhere — never .NET's default banker's rounding. See
[03 — The Calculation Pipeline](03-calculation-pipeline.md#step-6--net-pay-and-the-rounding-contract)
for why *when* you round (per-component, before deriving net) matters as much as *how*.

---

## Don't "simplify away" the legal encoding

> Do not "simplify away" annualization, allowance handling, low-income exemptions, or per-period
> table logic — these encode legal rules. If a tax rule looks odd, check the matching test before
> changing it.

Three concrete examples already documented as **intentional and protected**, not bugs waiting to
be fixed:

| Quirk | Where | Why it stays |
|---|---|---|
| California's 3-cent single-status adjustment | `CaliforniaWithholdingCalculator` | Matches the published Method B worksheet exactly; removing it would silently under- or over-withhold every California Single-filer paycheck by 3 cents |
| Oklahoma's whole-dollar rounding | `OklahomaOw2PercentageCalculator` | Form OW-2 specifies whole-dollar withholding, not cent rounding |
| Alabama's dependency on federal withholding | `AlabamaWithholdingCalculator` | The state's own formula genuinely requires the federal number — it's why `PayCalculator` computes federal before state at all |

See [05 — The State Withholding Engine](05-state-withholding-engine.md#documented-intentional-quirks)
for the full detail. The pattern generalizes: **an odd-looking number in a calculator is a
citation waiting to be read, not a cleanup opportunity**, until you've verified otherwise against
the actual published rule and updated the corresponding test.

---

## Adding or updating a state — the four-things-in-sync rule

Repeated verbatim across `CLAUDE.md`, `AGENTS.md`, and every relevant instruction file:

> When adding/changing state inputs, keep all four in sync: schema, validation, UI field
> resolution, and tests.

Concretely:

1. `Core/Data/Schemas/<state>.json` — the field definitions
2. The calculator's `Validate(StateInputValues)` — cross-field business rules
3. The calculator's `Calculate` — the `GetValueOrDefault<T>` reads
4. `<StateName>WithholdingCalculatorTest.cs` — explicit numeric golden vectors

The UI needs **no** change, in either front-end, because both render from the schema. See
[05](05-state-withholding-engine.md#adding-a-new-state-or-updating-an-existing-one) for the full
six-step procedure including the manifest update.

---

## Renaming a tax data file — the five-file rule

Also repeated everywhere, because it fails silently at runtime rather than at compile time:

> If you rename a JSON file, update every linker entry (Tests, Blazor, App), the loader in
> `AddPaycheckCalculatorCore`, and the tests that reference it.

Five places, in one change: `Core.csproj`, `Blazor.csproj`, `App.csproj`, `Tests.csproj`, and the
`dataReader.ReadAllText(...)` call site. See
[09 — Tax Data Files](09-tax-data-files.md#the-three-way-asset-wiring-problem) for exactly what
breaks (and where) when one of the five is missed.

---

## DTO changes are contract changes

Shared's instructions are explicit:

> Treat DTO changes as contract changes. Prefer additive fields with safe defaults over breaking
> renames/removals.

A `SavedPaycheckDto` or `BudgetDto` field change affects **four consumers simultaneously** — MAUI,
Blazor, the API, and every already-synced row already stored server-side. Adding a field with a
sensible default is safe; renaming or removing one is not, because it can break deserialization of
data that already exists in the wild (or, in this project's case, in a running PostgreSQL
database or an on-device JSON file from a previous app version).

`StateInputValues` gets its own explicit callout in every relevant instruction file:

> `StateInputValues` must round-trip as real CLR primitive values. `StateInputValuesJsonConverter`
> enforces this — never allow `JsonElement` to leak into consumers.

See [10 — Shared Contracts & Sync](10-shared-contracts-and-sync.md#stateinputvaluesjsonconverter)
for exactly why a naive JSON round-trip would silently break every state calculator's input
reading.

---

## Testing conventions

From `PaycheckCalculator.Tests/AGENTS.md` and `.github/instructions/tests.instructions.md`,
consistent with [14 — The Test Suite](14-test-suite.md):

- **Scenario-based names** that describe the rule being protected, not the method under test —
  tests are "living documentation."
- **Explicit numeric expected values** from the source rule, table, or hand calculation — never
  computed by calling the same (or another) production helper, which could just as easily
  reproduce the same bug.
- **Cover the edges deliberately**: bracket boundaries, exemption thresholds, allowances, extra
  withholding, pre-tax deduction effects, rounding, YTD wage-base caps, and state-specific
  exceptions.
- **When you change a calculator, update its corresponding test file** in the same change — not
  as a follow-up.
- **Never delete a test to make a failure disappear.** If a test is wrong, fix the test with a
  cited reason; if the behavior changed intentionally, update the expected value with the same
  discipline as writing it the first time.

---

## Budgeting and sync changes touch more surfaces than they look like

`docs/wiki/Contributing.md` lists the full fan-out for two categories of change that are easy to
under-scope:

**Any budgeting behavior change** potentially touches: Core domain models, MAUI's
`BudgetViewModel`/`BudgetPage`, Blazor's `Budget.razor`, Shared DTOs and stores, the sync API's
entities/endpoints, tests, and documentation.

**Any sync payload change** potentially touches: Shared DTOs and JSON behavior, store
abstractions and their implementations, the API endpoint and its EF Core entity model, a new
migration, merge logic and its tests, both front-ends' callers, and documentation.

Neither list is "nice to have" — it's the actual dependency fan-out these features have, given the
architecture described in [08](08-budgeting.md) and [10](10-shared-contracts-and-sync.md)–[11](11-sync-api.md).

---

## Things to actively avoid

Consolidated from `docs/wiki/Contributing.md`'s "Things to Avoid" and the per-project
`AGENTS.md`/instruction files:

- Adding UI, HTTP, or persistence dependencies to Core.
- Business logic in code-behind, value converters, chart drawables, or export launchers/services
  (exporters render bytes from already-computed results; they never recalculate).
- Reloading static tax JSON on every calculation — it's loaded once, at startup, through DI.
- Renaming tax JSON files without updating every asset/link/load/test reference.
- Removing tests to make a failure disappear.
- Changing target frameworks or preview package versions unless the task explicitly requires it.
- Introducing floating-point math into any money calculation.
- Hardcoding a per-state UI control when the schema mechanism can express the same input.

---

## When docs and code disagree

Every instruction source states the same tie-break rule identically:

> If code and docs disagree, trust the implementation and tests first, then repair docs
> separately.

This reference document was written by reading the implementation and its tests directly, and
should be kept current the same way — if a future change makes a claim here stale, the fix is to
update this chapter's source of truth (the code), then this documentation, not the reverse.

---

## The dual agent-instruction files

`CLAUDE.md` (for Claude Code) and `AGENTS.md` (for other coding agents, e.g. Codex) are
**deliberately parallel** — `AGENTS.md` states outright that it "mirrors `CLAUDE.md`; keep the two
in sync when project guidance changes." Six more `AGENTS.md` files, one per project directory,
narrow that same guidance to project-specific rules (reproduced in relevant form throughout this
reference — see especially the boundary rules quoted in
[01](01-solution-and-build.md#dependency-graph-and-layering-rules)).

`.github/copilot-instructions.md` and the seven `applyTo`-scoped files under
`.github/instructions/*.md` cover the identical ground again for GitHub Copilot, each instruction
file scoped by glob to exactly the project it governs (`PaycheckCalculator.API/**/*.cs`,
`PaycheckCalculator.Core/Data/**/*.json`, etc.).

The practical takeaway: **this project's conventions are documented redundantly, on purpose**,
so that whichever AI coding tool a contributor uses, the same rules surface. If you change a
convention, the honest fix touches all of these files, not just one.

---

## Documentation expectations

From `docs/wiki/Contributing.md`, restated as the standard this reference set itself is held to:

> Update documentation when changing: project structure, public features, calculation flow, state
> coverage, tax data files, budgeting/report behavior, sync payloads or persistence, front-end
> navigation, class/architecture relationships. Documentation should describe the current
> implementation, not a planned future shape.

That last sentence is the important one. `ROADMAP.md` is where planned-but-not-built direction
lives (tax-year versioning, broader CI, production sync hardening, planning tools — see its
"Delivery map" for the current milestone ordering). This reference set, `docs/wiki/`, and
`docs/class-diagram.md` describe **only what exists in the repository today**.

---

## Where to go next

| If you need to... | Read |
|---|---|
| Build, test, or run any project | [01 — Solution, Projects & Build](01-solution-and-build.md) |
| Understand a domain type | [02 — Core Domain Model](02-core-domain-model.md) |
| Trace how one paycheck is computed | [03 — The Calculation Pipeline](03-calculation-pipeline.md) |
| Add or fix a state's withholding rule | [05 — The State Withholding Engine](05-state-withholding-engine.md) |
| Understand gross-up, bonus, or self-employment mode | [06 — Alternate Calculators](06-alternate-calculators.md) |
| Change a citation or accuracy note | [07 — Explanations & Source Governance](07-explanations-and-source-governance.md) |
| Add a synced field or a new sync collection | [10](10-shared-contracts-and-sync.md) + [11](11-sync-api.md) |
| Touch either front-end | [12 — Blazor](12-blazor-web-app.md) / [13 — MAUI](13-maui-app.md) |
| Write or extend a test | [14 — The Test Suite](14-test-suite.md) |

For the product-level, task-oriented equivalent of this reference set, see
[`docs/wiki/`](../wiki/Home.md) — start at `Home.md`. For the type-relationship view, see
[`docs/class-diagram.md`](../class-diagram.md).
