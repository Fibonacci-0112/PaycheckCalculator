# PaycheckCalculator Roadmap

_Last updated: August 13, 2026_

This is the living product and engineering roadmap for PaycheckCalculator. It describes intended direction, not a promise of dates. Milestones are ordered by dependency and risk; accuracy, explainability, and data safety take priority over feature count.

## Product direction

PaycheckCalculator should become the most trustworthy, understandable, and practical US paycheck-planning tool for employees and independent contractors.

The product should remain:

- **Accuracy-first** — every supported calculation is traceable to an authoritative rule or table and protected by explicit test cases.
- **Explainable** — users can see how each result was calculated and which tax-year source supports it.
- **Private by default** — core calculations work without an account, and local/offline use remains a first-class path.
- **Cross-platform** — MAUI and web experiences share one calculation engine and reach feature parity deliberately.
- **Honest about scope** — estimates, unsupported local taxes, and assumptions are disclosed instead of hidden behind a precise-looking number.

Core calculation accuracy and source explanations should never become paid-only features.

## Current baseline

The repository already has a substantial foundation:

- Federal withholding, FICA, and state withholding for all 50 states plus the District of Columbia.
- Hourly and salary pay, deductions, annual projections, gross-up, supplemental wages, hourly/salary conversion, and self-employment estimates.
- MAUI clients for Android, iOS, Mac Catalyst, and Windows, a Blazor Server app, and an optional account/sync API.
- Saved-paycheck comparison, CSV/PDF export, budgeting, recurring bills, savings goals, and reports.
- A UI-agnostic Core project, shared sync contracts, schema-driven state inputs, xUnit tests, and CodeQL.

The next phase should consolidate that breadth into a dependable product. The repository currently has no open GitHub issues, so milestone work should be converted into focused issues before implementation begins.

## Delivery map

| Order | Milestone | Primary outcome | Planning size |
|---|---|---|---|
| 0 | Trust baseline | Make correctness and source provenance measurable | 2–4 weeks |
| 1 | Tax-year platform | Replace the single-year design with versioned tax data | 4–8 weeks |
| 2 | Reliable v1 foundation | Broaden CI, reduce UI duplication, and automate releases | 6–10 weeks |
| 3 | Production sync | Harden identity, persistence, operations, and conflict handling | 6–10 weeks |
| 4 | Planning tools | Add the highest-value user features on the stable foundation | Incremental |
| 5 | Platform growth | Accessibility, additional platforms, and sustainable Pro features | Later |

Planning sizes assume focused development and should be revised after issues are estimated.

## Milestone 0 — Trust baseline

### 0.1 Tax-source provenance

- Add a machine-readable source manifest for federal rules and every state/DC calculator.
- Record tax year, publication title, official source URL, revision/effective date, implementation type, and last verification date.
- Surface source metadata in “Show Your Work” and exports where practical.
- Document approximations and exclusions explicitly, especially where withholding is being used as a proxy for annual tax liability.
- Add an accuracy-incident issue template and a process for correcting published calculations.

### 0.2 Verified calculation corpus

- Add golden test vectors from official worksheets and published examples.
- Cover bracket boundaries, low-income exemptions, allowances, extra withholding, wage-base crossings, rounding rules, and deduction taxability.
- Add one registration/schema/smoke test for every state and DC.
- Maintain regression cases for every confirmed production calculation defect.
- Add invariant/property tests for facts such as non-negative withholding, component tie-out, deterministic output, and gross-up convergence.

### 0.3 Documentation reconciliation

Documentation has been reconciled with the code and should retain these baselines:

- Core targets .NET 10 only.
- The API uses Npgsql/PostgreSQL in normal operation; SQLite is limited to integration tests.
- The main .NET CI workflow explicitly restores, builds, and tests `PaycheckCalculator.Tests`. Core, Shared, API, and Blazor are built transitively through its project references; MAUI is not built by that workflow.
- SDK version and roll-forward details should match `global.json`.

### Exit criteria

- Every production calculator has a source record and verification owner/date.
- All 51 jurisdictions pass registration, schema, and representative calculation tests.
- Known documentation contradictions are resolved.
- A calculation defect can be reported, reproduced, fixed, and documented through a defined workflow.

## Milestone 1 — Tax-year platform

The current engine is tightly coupled to 2026: TaxYearSupport exposes one year, DI loads year-specific filenames directly, explanations embed 2026, and self-employment due dates are hard-coded. This is the highest-leverage architectural change because tax data expires every year.

### 1.1 Versioned tax-data packs

- Introduce a TaxDataPack/TaxRuleSet model keyed by tax year and jurisdiction.
- Resolve federal, FICA, supplemental-wage, state, schema, citation, and due-date data through an ITaxRuleProvider rather than hard-coded filenames.
- Validate every pack at startup/build time for missing jurisdictions, malformed brackets, duplicate ranges, and unsupported schema fields.
- Fail closed with a clear “tax year unavailable” message instead of silently using another year.
- Keep old tax-year packs immutable after release except for documented corrections.

### 1.2 End-to-end tax-year identity

- Carry tax year through calculation inputs, explanations, saved snapshots, sync DTOs, exports, URLs, and UI selection.
- Preserve the tax year on saved paychecks so historical results remain reproducible.
- Add compatibility/version fields to serialized snapshots before payloads evolve further.
- Make quarterly-payment dates data-driven by tax year.

### 1.3 Reduce tax-asset wiring duplication

- Move repeated MAUI, Blazor, and test tax-asset item declarations into shared MSBuild props/targets or generate them from the pack manifest.
- Add a build-time check that every declared data asset is packaged for each applicable runtime.
- Provide a small validation CLI or test harness for importing the next year’s official tables.

### Exit criteria

- At least two tax-year packs can be loaded side by side in tests.
- 2026 saved results remain reproducible after another year is added.
- Adding a tax year does not require editing hard-coded filenames in multiple project files.
- Missing or partial tax data cannot produce a normal-looking result.

## Milestone 2 — Reliable v1 foundation

### 2.1 Supported runtime and dependency management

- ~~Move off preview runtimes onto a supported release.~~ **Done** — `global.json` pins the .NET 10 GA SDK (10.0.400, `latestFeature`), every TFM is `net10.0`/`net10.0-*`, and no preview packages remain.
- Adopt central package management and dependency lock files.
- Add automated dependency update PRs with test validation.
- Treat compiler/analyzer warnings as a managed backlog, then ratchet critical projects toward warnings-as-errors.
- Remove committed per-user project settings such as csproj.user files.

### 2.2 Complete CI coverage

Build a platform-aware matrix rather than invoking only the test project:

- Core, Shared, API, Blazor, and tests on Linux.
- MAUI Android restore/build on a supported Linux runner.
- MAUI Windows build on a Windows runner.
- Formatting/analyzers, unit and integration tests, CodeQL, dependency vulnerability review, and tax-pack validation.
- Coverage reporting for Core/Shared, plus mutation testing for the highest-risk calculation primitives.
- Publish test reports and build artifacts for failed-run diagnosis.

### 2.3 Decompose oversized presentation code

Current orchestration hotspots include approximately:

- 1,469 lines in Blazor Calculator.razor.
- 1,292 lines in MAUI CalculatorViewModel.
- 839 lines in Blazor Budget.razor.
- 552 lines in MAUI BudgetViewModel.

Refactor by user capability, not arbitrary line count:

- Extract calculation-mode coordinators, validators, state-input adapters, result presenters, saved-paycheck workflows, comparison workflows, and export preparation.
- Split Razor pages into focused components with narrow parameters.
- Move duplicated front-end orchestration into a UI-neutral application layer while keeping Core free of UI, HTTP, and persistence dependencies.
- Preserve thin platform-specific shells for navigation, file pickers, printing, and secure storage.
- Add component/view-model contract tests to prevent MAUI and Blazor feature drift.

### 2.4 Repeatable releases

- Adopt semantic versioning and an automatically generated changelog.
- Replace hard-coded application version values with build-supplied versions.
- Produce signed/reproducible release artifacts where the platform permits.
- Generate an SBOM and attach checksums to releases.
- Document rollback and tax-data correction releases separately from feature releases.

### Exit criteria

- Every shipped runtime has an explicit CI build.
- A clean checkout can restore, test, and produce release artifacts using documented commands.
- Critical calculation paths meet agreed coverage and mutation thresholds.
- Major MAUI and Blazor workflows no longer depend on single thousand-line orchestration files.

## Milestone 3 — Production-ready accounts and sync

Keep sync optional. Do not market it as production-ready until this milestone is complete.

### 3.1 API and identity hardening

- Remove the fallback PostgreSQL username/password; validate required production configuration at startup.
- Add production exception handling, HTTPS/forwarded-header policy, HSTS where appropriate, request-size limits, and secure response headers.
- Rate-limit registration, login, token refresh, and sync endpoints separately.
- Add email verification, password reset, lockout/abuse protections, token/session revocation, and device/session visibility.
- Enforce server-side validation for every synced field, not only collection counts and names.
- Version the API and define backward-compatibility rules before mobile releases depend on it.

### 3.2 Operability and data protection

- Add liveness/readiness endpoints, structured logs, tracing, metrics, and privacy-safe correlation IDs.
- Run database migrations as a controlled deployment step instead of unconditionally during application startup.
- Define backup, restore, retention, and disaster-recovery procedures; test restoration.
- Add user data export and account/data deletion.
- Create a threat model and review against an appropriate OWASP ASVS baseline.

### 3.3 Conflict-safe sync

The current deterministic last-write-wins design trusts client timestamps. Improve it before usage scales:

- Add server revisions/ETags and optimistic concurrency.
- Make writes idempotent and clock-skew tolerant.
- Add delta sync and pagination instead of returning complete collections indefinitely.
- Define tombstone retention and safe compaction.
- Detect and explain conflicts that cannot be merged without data loss.
- Test retries, concurrent devices, offline edits, stale clients, partial failures, and clock skew.

### Exit criteria

- No production secret or credential has a usable fallback.
- Auth endpoints resist basic brute-force and abuse scenarios.
- Backup restoration and account deletion are exercised in automated or documented drills.
- Two offline devices can reconcile supported edits without silent loss in the tested conflict matrix.

## Milestone 4 — High-value planning features

Implement these in order unless user research changes the ranking.

| Priority | Feature | User outcome | Dependency |
|---|---|---|---|
| P1 | Paycheck audit | Compare an actual pay stub with the expected result and identify the line causing a discrepancy | Trust baseline |
| P1 | W-4 withholding optimizer | Estimate annual federal liability and suggest transparent W-4 adjustments to reach a refund/balance target | Multi-year federal liability engine |
| P1 | Complete 1099 planner | Add federal income tax, the deductible half of SE tax, filing-status assumptions, and safe-harbor estimates | Annual federal liability engine |
| P1 | Multi-state and reciprocity | Model residence state, work state, nonresident withholding, and reciprocity rules | Versioned jurisdiction model |
| P2 | Local wage taxes | Add work/residence locality support in carefully verified regional batches | Location model and maintainable source pipeline |
| P2 | Real-world earnings | Tips, commissions, shift differentials, retro pay, multiple overtime rates, and irregular hours | Input model versioning |
| P2 | Benefits modeling | Guided 401(k)/403(b), Roth, HSA/FSA, Section 125, garnishment, and employer-match scenarios | Deduction taxonomy |
| P2 | Payroll calendar | Use actual pay dates, remaining checks, 27/53-period years, bonuses, and mid-year changes | Tax-year/calendar services |
| P2 | Household/multiple jobs | Compare combined withholding for spouses and multiple employers | Annual liability engine |
| P2 | Offline-capable web | ~~Persist anonymous Blazor data in the browser~~ **Done** (localStorage behind `SessionPaycheckStore`/`SessionBudgetStore`); still to do: an installable PWA (manifest + service worker) | Snapshot versioning |
| P3 | Budget cash-flow calendar | Connect pay dates, recurring bills, sinking funds, and goals to forecast account needs | Payroll calendar |
| P3 | Tax-change alerts | Notify users when a selected jurisdiction/year changes and explain the impact | Tax-pack release metadata |

### Feature notes

**Paycheck audit** should accept manual line entry first. PDF/image import can follow only after privacy, extraction accuracy, and local-processing options are defined.

**W-4 optimization** must show assumptions and ranges. A recommendation without an annual liability model would merely optimize one withholding formula against itself.

**Local taxes** should launch in verified batches rather than claiming immediate nationwide coverage. A useful first batch can target high-demand systems such as New York City/Yonkers, Pennsylvania local earned-income taxes, Ohio municipalities, and selected county taxes.

**Self-employment planning** should distinguish payroll-style estimates from annual income-tax liability and should not imply preparation of a tax return.

## Milestone 5 — Platform growth and sustainable Pro features

- Meet WCAG 2.2 AA for the web experience and test keyboard, screen-reader, scaling, contrast, and error-state behavior on MAUI.
- Add iOS/macOS only after shared workflows, CI, packaging, and support ownership are ready.
- Define the entitlement matrix before wiring billing. Keep current-year calculations, sources, and accuracy explanations free; potential Pro value includes extended history, advanced comparisons, optimization scenarios, reports, and enhanced sync.
- Enforce entitlements server-side for hosted features and support receipt validation, grace periods, refunds, and restore-purchase flows.
- Consider a versioned public API/CLI only after authentication, quotas, source attribution, and compatibility policy are mature.
- Use privacy-preserving product analytics with opt-out controls to learn which inputs and explanations cause abandonment—never collect payroll values by default.

## Engineering debt register

| Finding | Evidence in the current repository | Planned response |
|---|---|---|
| Single-year coupling | TaxYearSupport supports only 2026; DI, filenames, explanations, and due dates embed the year | Milestone 1 tax-data packs |
| ~~Preview runtime graph~~ **Resolved** | global.json now pins the .NET 10 GA SDK (10.0.400); all TFMs are net10.0 and no preview packages remain | Remaining follow-on: central package management and lock files (2.1) |
| CI blind spots | dotnet.yml invokes the test project and has no explicit MAUI/platform matrix | Milestone 2 CI matrix |
| UI orchestration size | Calculator.razor is ~1,900 lines and CalculatorViewModel ~1,425; both keep growing as calculation modes are added | Capability-based application/presentation refactor |
| Repeated asset wiring | Tax JSON is separately declared for MAUI, Blazor, and tests | Manifest-driven MSBuild wiring |
| Documentation drift | Target-framework and database-provider descriptions disagree with current project files/code | Milestone 0 docs reconciliation |
| Client-clock conflict resolution | Sync merge uses client DateTimeOffset values for last-write-wins and retains tombstones | Server revisions, delta sync, and compaction policy |
| API production gaps | Fallback database credentials, startup migrations, and no visible rate-limit/health pipeline | Milestone 3 hardening |
| Manual release identity | MAUI version fields are hard-coded and no release workflow is present | Versioned release pipeline and SBOM |

## Shared definition of done

A roadmap item is not complete until applicable requirements are met:

- Acceptance cases and failure behavior are documented.
- Tax behavior cites authoritative sources and includes explicit expected-value tests.
- Core logic is shared; MAUI and Blazor do not independently reimplement it.
- Both front-ends expose the feature or the platform exception is documented.
- Saved snapshots, sync compatibility, exports, and explanations are evaluated.
- Accessibility and privacy impacts are reviewed.
- README/wiki/architecture documentation is updated.
- CI is green and release notes identify user-visible assumptions or changes.

## Success measures

Track a small set of measures that reward trust rather than vanity:

- Verified jurisdictions for the active tax year: **51 of 51**.
- Official/golden example pass rate: **100%**.
- Shipped runtime build coverage in CI: **100%**.
- Unresolved confirmed P0/P1 calculation defects at release: **0**.
- Sync conflict test-matrix pass rate before production launch: **100%**.
- Web accessibility: **WCAG 2.2 AA** on critical flows.
- Accuracy reports with reproducible test cases and an initial maintainer response: target service level defined before v1.

## Deliberately not now

These ideas add major legal, security, or operational scope and should not distract from the milestones above:

- Filing tax returns or acting as a payroll processor.
- Direct bank-account aggregation.
- Employer payroll administration and remittance.
- AI-generated tax advice.
- Nationwide local-tax claims without a maintainable authoritative data pipeline.
- Organization/team accounts before single-user sync is production-ready.

## Roadmap maintenance

- Review this document monthly and after every tax-data correction.
- Convert the active milestone into scoped GitHub issues with an owner, acceptance criteria, dependencies, and size.
- Label work by priority, area, kind, and tax year.
- Keep only one milestone “Now”; moving work forward requires an explicit tradeoff.
- Record completed outcomes in release notes rather than allowing this file to become a second changelog.
