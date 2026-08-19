# PaycheckCalculator — Codebase Reference

A complete, file-level walkthrough of the PaycheckCalculator codebase: what every project
contains, how the calculation engine works, how data flows from a UI keystroke to a rendered
paycheck line, and how the optional account/sync stack fits together.

This is the **deep reference**. It complements, rather than replaces:

| Document | Purpose |
|---|---|
| [`README.md`](../../README.md) | Product overview and quick start |
| [`docs/wiki/`](../wiki/Home.md) | Task-oriented guides (getting started, UI guide, contributing) |
| [`docs/class-diagram.md`](../class-diagram.md) | Mermaid UML of the type graph |
| **`docs/reference/` (this set)** | Exhaustive per-subsystem explanation of how the code works |

Where docs and code disagree, the code and its tests win — see
[15 — Conventions & Workflows](15-conventions-and-workflows.md).

---

## Reading order

Start at the top if you are new. Jump directly to a chapter if you are chasing a specific area.

### Foundations

1. **[Solution, Projects & Build](01-solution-and-build.md)**
   The six projects, their dependency graph and layering rules, target frameworks, the pinned
   preview SDK, NuGet dependencies, CI workflows, and the local/container run scripts.

2. **[Core Domain Model](02-core-domain-model.md)**
   Every type in `PaycheckCalculator.Core/Models/` — `PaycheckInput`, `PaycheckResult`,
   `Deduction` and its three independent tax-base flags, the enums, `UsState`, `TaxYearSupport` —
   plus `PayPeriods` and `PaycheckInputValidator`.

### The calculation engine

3. **[The Calculation Pipeline](03-calculation-pipeline.md)**
   `PayCalculator` step by step: gross pay, the three separate taxable-wage bases, ordering,
   the rounding contract that makes net pay balance to the cent, and how the explanation tree
   is assembled.

4. **[Federal Withholding & FICA](04-federal-and-fica.md)**
   `Irs15TPercentageCalculator` (Pub 15-T Worksheet 1A, automated payroll systems),
   `FicaCalculator` (Social Security wage base, Medicare, Additional Medicare), and
   `FederalSupplementalCalculator` (Pub 15 §7 flat-rate method).

5. **[The State Withholding Engine](05-state-withholding-engine.md)**
   `IStateWithholdingCalculator`, `StateCalculatorRegistry`, the schema-driven UI contract,
   the four calculator categories, the complete 51-jurisdiction coverage table, state
   disability / paid-leave lines, and the documented intentional quirks.

6. **[Alternate Calculators](06-alternate-calculators.md)**
   `GrossUpCalculator` (bisection solver), `BonusCalculator`, `SelfEmploymentCalculator`,
   `AnnualProjectionCalculator`, and `HourlySalaryCalculator`.

7. **[Explanations & Source Governance](07-explanations-and-source-governance.md)**
   The "Show Your Work" object model, `TaxSourceCatalog`, the manifest's startup validation
   gauntlet, accuracy notes, and the accuracy-incident process.

8. **[Budgeting](08-budgeting.md)**
   `BudgetCalculator`, `BudgetReportCalculator`, allocation rules, recurring bills, savings
   goals, monthly-income normalization, and Pro entitlement gating.

9. **[Tax Data Files](09-tax-data-files.md)**
   Every JSON file in `Core/Data/`, its schema, its loader, and the three-way asset wiring
   (MAUI package assets, Blazor `TaxData/`, test content links) you must keep in sync.

### Accounts, sync, and the front-ends

10. **[Shared Contracts & Sync](10-shared-contracts-and-sync.md)**
    DTOs, tombstones, the deterministic last-write-wins mergers, `PaycheckJson` and the
    `StateInputValues` converter, `PaycheckApiClient`, store abstractions, entitlements.

11. **[The Sync API](11-sync-api.md)**
    ASP.NET Core Identity endpoints, the authorized sync/export/delete endpoints, the EF Core
    model and migrations, PostgreSQL vs SQLite, and the request-validation limits.

12. **[The Blazor Web App](12-blazor-web-app.md)**
    Interactive Server render mode, circuit-scoped state, the calculator page, SEO landing
    pages and sitemap, export/print via JS interop, and the export renderers.

13. **[The MAUI App](13-maui-app.md)**
    Shell tab structure, the MVVM view models and CommunityToolkit source generators, mappers
    and presentation models, on-device storage, sync coordination, and the dependency-free
    PDF/CSV/print exporters.

### Working on the code

14. **[The Test Suite](14-test-suite.md)**
    How the ~1,388 xUnit tests are organized, the architecture-conformance tests, the golden
    corpus, API integration tests, and the rules for writing new tests.

15. **[Conventions & Workflows](15-conventions-and-workflows.md)**
    The layering rules, the `decimal` mandate, how to add or update a state, how to roll a tax
    year, agent-instruction files, and the documentation contract.

---

## The 60-second version

PaycheckCalculator turns pay/W-4/state/deduction inputs into a fully itemized paycheck for the
**2026** tax year, across all 50 US states plus the District of Columbia.

```text
                    ┌──────────────────────────────┐
   MAUI app  ───────▶                              │
   (Android/iOS/    │   PaycheckCalculator.Core    │  ← all tax math, no UI/HTTP/persistence
    macOS/Windows)  │                              │
                    │   PayCalculator orchestrates │
   Blazor web ──────▶   FICA + federal + state     │
   (Server)         └──────────────────────────────┘
        │                        ▲
        │                        │ ITaxDataReader (platform-supplied)
        │                        │
        │              Core/Data/*.json  (IRS 15-T, per-state tables,
        │                                 51 UI schemas, source manifest)
        │
        └──▶ PaycheckCalculator.Shared ──▶ PaycheckCalculator.API ──▶ PostgreSQL
             (DTOs, mergers, HTTP client)   (Identity + sync endpoints)
```

Four ideas carry most of the design:

1. **Core is UI-agnostic and money is always `decimal`.** No MAUI, no HTTP, no EF Core, no
   file system. Both front-ends call the same `AddPaycheckCalculatorCore` composition root and
   differ only in the `ITaxDataReader` they supply.

2. **`PayCalculator` composes; it never absorbs.** Gross pay, FICA, federal, and state are
   separate collaborators. State-specific law lives in exactly one place per state.

3. **State UI is schema-driven.** Each jurisdiction ships a `Data/Schemas/<state>.json` file
   describing its input fields; both front-ends render those dynamically. Adding a state input
   is a data change plus a calculator change — never a hard-coded control.

4. **Every number can explain itself and cite its source.** Results carry a
   `PaycheckExplanation` tree of worksheet-style steps, each linked to a validated entry in
   `tax_source_manifest_2026.json` that names the official publication, its URL, its
   verification date, and its known approximations and exclusions.

---

## Scale at a glance

| Metric | Value |
|---|---|
| Projects | 6 (`Core`, `Shared`, `Api`, `Blazor`, `App`, `Tests`) |
| C# files | 341 |
| Jurisdictions supported | 51 (50 states + DC) |
| Dedicated state calculator classes | 44 |
| No-income-tax states via shared adapter | 7 (AK, FL, NV, NH, SD, TN, TX) |
| State UI schema files | 51 |
| Source-manifest rules | 112 |
| xUnit `[Fact]`/`[Theory]` attributes | ~1,388 across 83 test files |
| Tax year supported | 2026 only (`TaxYearSupport.CurrentTaxYear`) |

Counts reflect the tree at the time of writing; the reconciliation logic that keeps them honest
is described in [05 — The State Withholding Engine](05-state-withholding-engine.md#coverage-reconciliation)
and [14 — The Test Suite](14-test-suite.md).
