# PaycheckCalculator Roadmap

_Last updated: September 10, 2026._

This is the living product and engineering roadmap for PaycheckCalculator, and the **single plan of
record**. It describes intended direction, not a promise of dates. Milestones are ordered by
dependency and risk; accuracy, explainability, and data safety take priority over feature count.

A commercial review from August 2026 is archived at
[`docs/reviews/2026-08-commercial-review.md`](docs/reviews/2026-08-commercial-review.md). Its live
recommendations are folded into this document — chiefly Milestones 3 and 5, which did not previously
exist. That review is history and is not maintained; where it disagrees with this file, this file wins.

## Status

| | Milestone | Why now |
|---|---|---|
| **Now** | **1 — Tax-year platform** | The only item on this roadmap with an external deadline. The engine is hardcoded to 2026 and fails closed on every other year, so the product stops working for the 2027 season unless this lands first. |
| **Next** | 2 — Reliable v1 foundation, then 3 — Ship and distribute | Nothing is deployed. Until it is, no question about traffic, conversion, or pricing can be answered with data rather than opinion. |
| **Later** | 4 — Production accounts and sync · 5 — Revenue · 6 — Planning tools · 7 — Platform growth | Each is gated on something above it. |

Only one milestone is "Now" at a time. Moving work forward out of order requires an explicit tradeoff,
recorded here.

## Product direction

PaycheckCalculator should become the most trustworthy, understandable, and practical US
paycheck-planning tool for employees and independent contractors.

The product should remain:

- **Accuracy-first** — every supported calculation is traceable to an authoritative rule or table and protected by explicit test cases.
- **Explainable** — users can see how each result was calculated and which tax-year source supports it.
- **Private by default** — core calculations work without an account, and local/offline use remains a first-class path.
- **Cross-platform** — MAUI and web experiences share one calculation engine and reach feature parity deliberately.
- **Honest about scope** — estimates, unsupported local taxes, and assumptions are disclosed instead of hidden behind a precise-looking number.

Core calculation accuracy and source explanations should never become paid-only features.

## Current baseline

Verified against `b000521` on September 10, 2026:

- Federal withholding, FICA, and state withholding for **51 jurisdictions** (50 states + DC), each with a dedicated calculator and input schema.
- Six calculation pipelines: standard, gross-up, supplemental/bonus, self-employment, hourly↔salary conversion, and annual projection.
- A **119-rule source manifest** citing the official 2026 publication behind each number, validated at startup.
- **1,613 tests, all passing**, in ~4 seconds. The test project (27.7k lines) is larger than Core (15.7k).
- Clean build: 0 warnings, 0 errors across Core, Shared, API, Blazor and tests. The MAUI Android target builds with 5 known warnings.
- MAUI clients for Android, iOS, Mac Catalyst and Windows; a Blazor Server web app; an optional account/sync API.
- Saved-paycheck comparison, CSV/PDF export, budgeting, recurring bills, savings goals, and reports.

**Nothing is deployed.** There is no public instance, no users, and therefore no usage data. This is
the single most important fact for sequencing: it is what places Milestone 3 ahead of Milestone 5.

The repository has **no open GitHub issues and has never had any**. Milestone work should be converted
into focused issues before implementation begins — a document is not a work queue, and its absence is
a large part of why the next step is hard to see from a cold start.

## Delivery map

| Order | Milestone | Primary outcome | Planning size |
|---|---|---|---|
| 0 | Trust baseline | Make correctness and source provenance measurable | Largely complete |
| **1** | **Tax-year platform** | Replace the single-year design with versioned tax data | 4–8 weeks |
| 2 | Reliable v1 foundation | Broaden CI, reduce UI duplication, automate releases | 6–10 weeks |
| 3 | Ship and distribute | Get it deployed, reachable, and measurable | 3–6 weeks |
| 4 | Production accounts and sync | Harden identity, persistence, operations, conflict handling | 6–10 weeks |
| 5 | Revenue | A path from willing customer to paid, enforced server-side | 4–8 weeks |
| 6 | Planning tools | The highest-value user features on a stable foundation | Incremental |
| 7 | Platform growth | Accessibility, additional platforms, sustainable Pro tiers | Later |

Planning sizes assume focused development and should be revised after issues are estimated.

## Milestone 0 — Trust baseline

Substantially complete. Retained because its exit criteria are standing obligations, not one-off tasks.

### 0.1 Tax-source provenance — **done**

- ~~Add a machine-readable source manifest for federal rules and every state/DC calculator.~~ `Data/tax_source_manifest_2026.json`, 119 rules.
- ~~Record tax year, publication title, official source URL, revision/effective date, implementation type, and last verification date.~~ All present per rule.
- ~~Surface source metadata in "Show Your Work" and exports where practical.~~
- ~~Document approximations and exclusions explicitly.~~ `approximations` / `exclusions` per rule, surfaced as `AccuracyNote`s in both front-ends.
- ~~Add an accuracy-incident issue template and a correction process.~~ `.github/ISSUE_TEMPLATE/accuracy-incident.yml` and `docs/wiki/Accuracy-and-Source-Governance.md`.

Standing obligation: `TaxSourceCatalog.Validate` / `ValidateCalculatorRegistrations` must keep failing
loudly on a missing or malformed entry, so an uncited number cannot ship quietly.

### 0.2 Verified calculation corpus

- ~~Add one registration/schema/smoke test for every state and DC.~~ `VerifiedCalculationCorpusTest.ProductionRegistry_AllJurisdictionsHaveSchemaAndSmokeResult` asserts the registry covers every `UsState`, that each schema has unique keys, and that no jurisdiction returns negative wages, withholding, or assessments.
- ~~Add invariant tests for component tie-out and determinism.~~ `ProductionPaycheckCorpus_TiesOutAndIsDeterministicForEveryJurisdiction`.
- **Open:** golden test vectors taken from official worksheets and published examples, beyond the states already covered that way.
- **Open:** property tests for gross-up convergence.
- Standing obligation: a regression case for every confirmed production calculation defect.

### 0.3 Documentation reconciliation — **regressed, needs a sweep**

The baselines below are correct in `README.md`, `CLAUDE.md` and the root `AGENTS.md`, and **wrong**
in the six per-project `AGENTS.md` files, `docs/wiki/{Home,Getting-Started,Contributing}.md`,
`docs/reference/01-solution-and-build.md`, and `replit.md`, all of which still describe .NET 11 and a
preview SDK pin:

- Core targets **.NET 10** only; the SDK pin is `10.0.400` with `latestFeature` roll-forward.
- The API uses Npgsql/PostgreSQL in normal operation; SQLite is limited to integration tests.
- CI restores, builds and tests `PaycheckCalculator.Tests`, building Core, Shared, API and Blazor transitively, and separately builds the MAUI Android target.

`README.md` also under-reports state coverage: it lists four disability/paid-leave states where ten
are implemented.

### Exit criteria

- Every production calculator has a source record and verification owner/date. **Met.**
- All 51 jurisdictions pass registration, schema, and representative calculation tests. **Met.**
- Known documentation contradictions are resolved. **Not met** — see 0.3.
- A calculation defect can be reported, reproduced, fixed, and documented through a defined workflow. **Met.**

## Milestone 1 — Tax-year platform — **NOW**

The engine is tightly coupled to 2026. `TaxYearSupport.CurrentTaxYear` is a `const`, `SupportedTaxYears`
is a one-element array, `PayCalculator.Calculate` throws `NotSupportedException` on any other year, eleven
data files carry `_2026` in their names, and 1040-ES due dates are hardcoded. This is the highest-leverage
architectural change because tax data expires every year — and it is the only work here with a real deadline.

### 1.1 Versioned tax-data packs

- Introduce a `TaxDataPack`/`TaxRuleSet` model keyed by tax year and jurisdiction.
- Resolve federal, FICA, supplemental-wage, state, schema, citation, and due-date data through an `ITaxRuleProvider` rather than hardcoded filenames.
- Validate every pack at startup/build time for missing jurisdictions, malformed brackets, duplicate ranges, and unsupported schema fields.
- Keep failing closed with a clear "tax year unavailable" message instead of silently using another year. The current fail-closed behavior is correct and must survive the refactor.
- Keep old tax-year packs immutable after release except for documented corrections.

### 1.2 End-to-end tax-year identity

- Carry tax year through calculation inputs, explanations, saved snapshots, sync DTOs, exports, URLs, and UI selection.
- Preserve the tax year on saved paychecks so historical results remain reproducible.
- Add compatibility/version fields to serialized snapshots before payloads evolve further.
- Make quarterly-payment dates data-driven by tax year.

### 1.3 Reduce tax-asset wiring duplication

Each tax JSON file is declared separately in three places — `MauiAsset` items in the MAUI csproj,
`TaxData` links in the Blazor csproj, and content links in the test csproj — plus the DI loader.
Adding a year currently means touching all four.

- Move the repeated declarations into shared MSBuild props/targets, or generate them from the pack manifest.
- Add a build-time check that every declared data asset is packaged for each applicable runtime.
- Provide a small validation CLI or test harness for importing the next year's official tables.

### Exit criteria

- At least two tax-year packs can be loaded side by side in tests.
- 2026 saved results remain reproducible after another year is added.
- Adding a tax year does not require editing hardcoded filenames in multiple project files.
- Missing or partial tax data cannot produce a normal-looking result.

## Milestone 2 — Reliable v1 foundation

### 2.1 Supported runtime and dependency management

- ~~Move off preview runtimes onto a supported release.~~ **Done** — `global.json` pins the .NET 10 GA SDK (10.0.400, `latestFeature`), every TFM is `net10.0`/`net10.0-*`, and no preview packages remain.
- Adopt central package management and dependency lock files.
- Add automated dependency update PRs with test validation.
- Treat compiler/analyzer warnings as a managed backlog, then ratchet critical projects toward warnings-as-errors. The non-MAUI projects already build warning-free; the MAUI app carries 5 known warnings (4 `DisplayAlert` obsolescence, 1 target-SDK notice).
- **Apply `dotnet format`.** It currently reports roughly 400 whitespace violations across ~30 files in all four buildable projects — misindented members, stray blank lines, members at column 0. Nothing checks it, so it accumulates. Fix in one mechanical pass, then add `--verify-no-changes` to CI so it cannot come back.
- Remove committed per-user project settings such as csproj.user files.

### 2.2 Complete CI coverage

Build a platform-aware matrix rather than invoking only the test project:

- ~~Core, Shared, API, Blazor and tests on Linux.~~ **Done.**
- ~~MAUI Android build on a Linux runner.~~ **Done** — the `android` job in `dotnet.yml`. This closed a real gap: the MAUI app was merged broken at least once (#241 fixed a CS1503 that reached main because the change could not be compiled where it was written), and #243 shipped MAUI code its own description records as never compiled.
- MAUI Windows build on a Windows runner; iOS/Mac Catalyst on macOS. Still uncovered.
- Formatting/analyzer checks, dependency vulnerability review, and tax-pack validation.
- Coverage reporting for Core/Shared, plus mutation testing for the highest-risk calculation primitives.
- Publish test reports and build artifacts for failed-run diagnosis.
- **Watch CodeQL.** All three analysis jobs failed together on main between September 6 and 7 and nothing surfaced it; they pass again as of run 146. Simultaneous failure across three unrelated languages at the upload step points at infrastructure rather than code, but an unnoticed two-day outage in the security workflow is itself the finding.

### 2.3 Decompose oversized presentation code

Measured at `b000521`:

| File | Lines |
|---|---|
| `PaycheckCalculator.Blazor/Components/Pages/Calculator.razor` | 1,948 |
| `PaycheckCalculator.App/ViewModels/CalculatorViewModel.cs` | 1,474 |
| `PaycheckCalculator.Blazor/Components/Pages/Budget.razor` | 861 |
| `PaycheckCalculator.App/ViewModels/BudgetViewModel.cs` | 551 |

Both top files grow every time a calculation mode is added — there are now six.

Refactor by user capability, not arbitrary line count:

- Extract calculation-mode coordinators, validators, state-input adapters, result presenters, saved-paycheck workflows, comparison workflows, and export preparation.
- Split Razor pages into focused components with narrow parameters.
- Move duplicated front-end orchestration into a UI-neutral application layer while keeping Core free of UI, HTTP, and persistence dependencies.
- Preserve thin platform-specific shells for navigation, file pickers, printing, and secure storage.
- Add component/view-model contract tests to prevent MAUI and Blazor feature drift.

### 2.4 Repeatable releases

- Adopt semantic versioning and an automatically generated changelog.
- Replace hardcoded application version values with build-supplied versions. `ApplicationDisplayVersion` is `1.0` and `ApplicationVersion` is `1`, both literal.
- Produce signed/reproducible release artifacts where the platform permits.
- Generate an SBOM and attach checksums to releases.
- Document rollback and tax-data correction releases separately from feature releases.

### Exit criteria

- Every shipped runtime has an explicit CI build.
- A clean checkout can restore, test, and produce release artifacts using documented commands.
- Critical calculation paths meet agreed coverage and mutation thresholds.
- Major MAUI and Blazor workflows no longer depend on single thousand-line orchestration files.

## Milestone 3 — Ship and distribute

New in this revision, from the archived review's Parts 2 and 3. Nothing is deployed, so every question
about acquisition, conversion or pricing is currently unanswerable. This milestone makes them answerable.

### 3.1 Deploy something

- Stand up the Blazor app at a real domain, with TLS, a health check, and a documented redeploy path.
- Decide whether the sync API deploys alongside it or stays local-only for now. The calculator works fully without it.
- The Replit configuration in `.replit`/`replit.md` is the closest thing to a deployment story and is stale — it describes a .NET 11 preview SDK and a `dotnet-7.0` Nix module. Either repair it or replace it with the real target.

### 3.2 Settle the hosting model before traffic arrives

`Components/App.razor` applies `@rendermode="InteractiveServer"` to the whole router, and `Program.cs`
registers only the interactive-server render mode. Every anonymous visitor therefore opens and holds a
SignalR circuit backed by server memory. For a calculator whose expected traffic is high-bounce and
mostly mobile, that means cost scales with concurrent visitors rather than usage, interaction latency
depends on a socket round-trip, and a dropped connection breaks the page.

- Evaluate static SSR for landing and content pages with interactivity as islands — `InteractiveAuto`/WebAssembly for the calculator, or a stateless server-rendered form post.
- Reserve circuits for signed-in workflows where the cost is justified.
- Size this honestly: it is a significant refactor, and it determines the unit economics of every acquisition channel. Deciding it after traffic arrives is more expensive than deciding it now.

### 3.3 Make results addressable

Calculator state lives entirely in the circuit. There is no `?state=ca&salary=75000&frequency=biweekly`
representation, so results cannot be shared or bookmarked, ads cannot land on pre-filled intent, and
generated landing pages have nothing to link into.

- Add deep-link query parameters for the calculator's inputs, round-tripped through the URL.
- This is a small change that unblocks sharing, paid-acquisition testing and 3.4 simultaneously.

### 3.4 Deepen the SEO surface that already exists

51 state landing pages, a generated sitemap, canonical tags, OpenGraph and JSON-LD are already in place.
Four things hold them back:

- `wwwroot/robots.txt` ends with `Sitemap: /sitemap.xml`. The sitemaps protocol requires an absolute URL; crawlers are not obliged to resolve a relative path. Serve `robots.txt` from a minimal-API endpoint and interpolate scheme/host exactly as the sitemap route already does.
- `Home.razor` uses a relative canonical (`href="/"`) while `StateLandingPage.razor` uses an absolute one. Make both absolute.
- Each state page is ~5 lines of unique prose over an identical calculator — a recognizable thin-page pattern at 51 URLs. Generate real per-state substance from data already shipped: the 2026 bracket table, the disability/paid-leave line where one exists, a worked take-home table at several salary points, and reciprocity/local-tax caveats.
- Emit `FAQPage` structured data alongside the existing `WebApplication` schema.

### 3.5 Instrument the funnel

There is no analytics, telemetry or event logging anywhere in the solution. Before feature work resumes,
add a privacy-preserving funnel: `landed → calculated → saved → signed up → hit gate → converted`,
segmented by landing page and state.

- **Never log payroll values by default** — event names and coarse dimensions only.
- Given the product's privacy positioning, self-hosted event capture in the existing API is a better fit than a third-party pixel, and avoids a cookie-consent burden on every page.

### 3.6 Legal pages that survive review

`Privacy.razor` and `Terms.razor` are 20 lines each, most of it markup. That is below the bar for
payment-processor onboarding, ad-network approval, and app-store review, and it blocks every revenue
path at once. Needed: data categories collected, retention periods, sub-processors, deletion mechanics,
cookie/advertising disclosure, children's-data statement, contact address, and an effective date.

### Exit criteria

- A real person can reach the calculator at a stable URL and get a correct answer.
- A calculation can be shared as a link that reproduces it.
- The funnel above produces numbers for a week of real traffic.
- Legal pages are complete enough to submit to a payment processor.

## Milestone 4 — Production-ready accounts and sync

Keep sync optional. Do not market it as production-ready until this milestone is complete.

### 4.1 API and identity hardening

- Remove the fallback PostgreSQL username/password. `Program.cs` falls back to `Username=admin;Password=password`, so a misconfigured deploy starts successfully on development credentials rather than failing loudly. Validate required production configuration at startup instead.
- Add production exception handling, HTTPS/forwarded-header policy, HSTS where appropriate, request-size limits, and secure response headers.
- Rate-limit registration, login, token refresh, and sync endpoints separately.
- Add email verification, password reset, lockout/abuse protections, token/session revocation, and device/session visibility.
- Enforce server-side validation for every synced field, not only collection counts and names.
- Version the API and define backward-compatibility rules before mobile releases depend on it.

### 4.2 Operability and data protection

- Add liveness/readiness endpoints, structured logs, tracing, metrics, and privacy-safe correlation IDs.
- Run database migrations as a controlled deployment step instead of unconditionally during application startup.
- Define backup, restore, retention, and disaster-recovery procedures; test restoration.
- Add user data export and account/data deletion.
- Create a threat model and review against an appropriate OWASP ASVS baseline.

### 4.3 Conflict-safe sync

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

## Milestone 5 — Revenue

New in this revision, from the archived review's Parts 1 and 4. Gated on Milestone 3 (you cannot price
what you cannot measure) and Milestone 4 (taking money makes the API's gaps financial risk rather than
technical debt).

### 5.1 A path from willing customer to paid

`FreeEntitlementProvider.IsPro` is a hardcoded `false`, registered in both front-ends. The only paywall
in the product renders the words "Pro feature" and a description, with no price, button, waitlist or
link. A user who has decided to pay cannot.

1. **Cheap first step:** turn the dead-end gate into an email capture — "Pro launches soon, $X/yr — get notified." It costs hours and produces the only two numbers that matter early: how many people reach the gate, and how many want it enough to leave an address. Run it at two or three price points.
2. **Then:** a checkout flow and webhook against `PaycheckCalculator.API`, with a subscriptions table keyed to the existing `IdentityUser`. `IEntitlementProvider` is already the right seam — swap the implementation, don't redesign it.
3. **Non-negotiable:** entitlement is currently presentation-only. `IsPro` gates markup, not data. The moment a paid tier exists, every Pro-only computation must be enforced server-side. A client flag is not a paywall.

### 5.2 What to actually sell

Free calculators are abundant, and nothing currently gated would make someone pay. Ranked by
willingness-to-pay against build cost:

1. **Paycheck audit — "why is my check short?"** The highest-intent moment in the category: someone holding a stub who thinks they have been shorted. The engine already produces a fully itemized expected paycheck with per-line explanations; the missing piece is a form for actual stub values and a diff view highlighting the line that disagrees. Also the top-ranked feature in Milestone 6.
2. **Complete 1099 / contractor planner.** Real money at stake, a recurring quarterly deadline, and an established habit of paying for tools in this class. `SelfEmploymentCalculator` already does SE tax and the 1040-ES schedule; the gap is federal income tax on the annual side.
3. **Multi-year history and unlimited saved paychecks.** Near-zero build — an entitlement check over data already stored and synced. Enabled by Milestone 1.
4. **W-4 optimizer.** Strong pull, but genuinely blocked on an annual liability engine. Do not ship a version that optimizes one withholding formula against itself.
5. **Budget reports.** Already built and already gated. A bundle sweetener, not the headline.

**Hold the line:** current-year accuracy, source citations and the explanation tree stay free. They are
the acquisition engine and the trust asset.

### 5.3 The other option — license the engine

A deliberate divergence from the original roadmap, which deferred any public API to "later."

The rare asset here is not the UI — it is a UI-agnostic, source-cited, 51-jurisdiction withholding engine
with 1,613 tests behind it. Payroll startups, HR platforms, staffing and contractor marketplaces and
fintechs all need exactly this, and mostly build it badly. A metered calculation API or licensed SDK
carries far higher revenue per customer than consumer subscriptions, needs no ad inventory, no SEO and
no app-store cut.

The caution is real — auth, quotas, compatibility policy and a support commitment are genuine obligations
that consumer traffic does not impose. But deferring it behind every other milestone likely defers the
highest-margin option in the repository by a year. **Validate demand cheaply and early:** a landing page
and ten customer conversations, run in parallel with whatever else is in flight. If the demand is real,
this milestone reorders.

### Exit criteria

- A customer can pay, and paying changes what the server returns, not just what the client renders.
- Entitlements are enforced in the API for every gated computation.
- Free-tier accuracy, citations and explanations are unchanged by the existence of a paid tier.

## Milestone 6 — High-value planning features

Implement these in order unless user research changes the ranking.

| Priority | Feature | User outcome | Dependency |
|---|---|---|---|
| P1 | Paycheck audit | Compare an actual pay stub with the expected result and identify the line causing a discrepancy | Trust baseline |
| P1 | W-4 withholding optimizer | Estimate annual federal liability and suggest transparent W-4 adjustments to reach a refund/balance target | Multi-year federal liability engine |
| P1 | Complete 1099 planner | Add federal income tax, the deductible half of SE tax, filing-status assumptions, and safe-harbor estimates | Annual federal liability engine |
| P1 | Multi-state and reciprocity | Model residence state, work state, nonresident withholding, and reciprocity rules | Versioned jurisdiction model |
| P2 | Local wage taxes | Add work/residence locality support in carefully verified regional batches | Location model; the `LocalWithholding` rule scope added for Maryland county tax is the seam |
| P2 | Real-world earnings | Tips, commissions, shift differentials, retro pay, multiple overtime rates, and irregular hours | Input model versioning |
| P2 | Benefits modeling | Guided 401(k)/403(b), Roth, HSA/FSA, Section 125, garnishment, and employer-match scenarios | Deduction taxonomy |
| P2 | Payroll calendar | Use actual pay dates, remaining checks, 27/53-period years, bonuses, and mid-year changes | Tax-year/calendar services |
| P2 | Household/multiple jobs | Compare combined withholding for spouses and multiple employers | Annual liability engine |
| P2 | Installable PWA | ~~Persist anonymous Blazor data in the browser~~ **Done** (localStorage behind `SessionPaycheckStore`/`SessionBudgetStore`); still to do: manifest + service worker | Snapshot versioning |
| P3 | Budget cash-flow calendar | Connect pay dates, recurring bills, sinking funds, and goals to forecast account needs | Payroll calendar |
| P3 | Tax-change alerts | Notify users when a selected jurisdiction/year changes and explain the impact | Tax-pack release metadata |

### Feature notes

**Paycheck audit** should accept manual line entry first. PDF/image import can follow only after
privacy, extraction accuracy, and local-processing options are defined.

**W-4 optimization** must show assumptions and ranges. A recommendation without an annual liability
model would merely optimize one withholding formula against itself.

**Local taxes** should launch in verified batches rather than claiming immediate nationwide coverage.
A useful first batch can target New York City/Yonkers, Pennsylvania local earned-income taxes, Ohio
municipalities, and selected county taxes.

**Self-employment planning** should distinguish payroll-style estimates from annual income-tax
liability and should not imply preparation of a tax return.

## Milestone 7 — Platform growth

- Meet WCAG 2.2 AA for the web experience and test keyboard, screen-reader, scaling, contrast, and error-state behavior on MAUI. For a US consumer-finance site this is legal exposure, not only a quality item, and should move earlier once anything is deployed.
- **Decide the MAUI question.** Four platforms of maintenance currently carry no store presence: `ApplicationId` is `com.erik.paycheckcalc`, versions are hardcoded, and only Android is built by CI. Either commit to it as a retention and subscription-delivery surface for existing users — which needs a real bundle identifier, build-supplied versioning, store listings and full CI coverage first — or freeze it and put the marginal effort into web. Leaving it undecided means paying parity costs on every feature without deciding whether they buy anything.
- Enforce entitlements server-side for hosted features and support receipt validation, grace periods, refunds, and restore-purchase flows.
- Use privacy-preserving product analytics with opt-out controls to learn which inputs and explanations cause abandonment — never collect payroll values by default.

## Engineering debt register

| Finding | Evidence at `b000521` | Planned response |
|---|---|---|
| Single-year coupling | `TaxYearSupport` supports only 2026; DI, filenames, explanations, and 1040-ES dates embed the year | Milestone 1 — **Now** |
| ~~Preview runtime graph~~ **Resolved** | `global.json` pins the .NET 10 GA SDK (10.0.400); all TFMs are `net10.0`; no preview packages | Remaining follow-on: central package management and lock files (2.1) |
| ~~MAUI never built by CI~~ **Resolved for Android** | The `android` job builds `net10.0-android`; the app had reached main broken at least once | Windows/macOS targets still uncovered (2.2) |
| Unenforced formatting | ~400 `dotnet format` whitespace violations across ~30 files; nothing checks it | One mechanical pass, then `--verify-no-changes` in CI (2.1) |
| UI orchestration size | `Calculator.razor` 1,948 lines; `CalculatorViewModel` 1,474; both grow per calculation mode | Capability-based refactor (2.3) |
| Repeated asset wiring | Tax JSON declared separately for MAUI, Blazor and tests, plus the DI loader | Manifest-driven MSBuild wiring (1.3) |
| Documentation drift | Six per-project `AGENTS.md`, three wiki pages, a reference chapter, and `replit.md` still describe .NET 11; `README.md` under-reports state coverage | Documentation sweep (0.3) |
| Hosting model | `InteractiveServer` on the whole router; every anonymous visitor holds a circuit | Decide before traffic (3.2) |
| No measurement | No analytics, telemetry, or event logging anywhere in the solution | Funnel instrumentation (3.5) |
| No purchase path | `IsPro` hardcoded `false`; the only gate is a dead end with no price or link | Milestone 5 |
| Client-clock conflict resolution | Sync merge uses client `DateTimeOffset` values for last-write-wins and retains tombstones | Server revisions, delta sync, compaction (4.3) |
| API production gaps | Fallback database credentials, startup migrations, no rate limiting or health endpoint | Milestone 4 hardening |
| Manual release identity | MAUI version fields hardcoded; no release workflow | Versioned release pipeline and SBOM (2.4) |
| Unwatched security workflow | All three CodeQL jobs failed on main for two days in September and nothing surfaced it | Alerting on workflow failure (2.2) |

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

Targets, not current readings. Track a small set that rewards trust rather than vanity:

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
- Convert the active milestone into scoped GitHub issues with an owner, acceptance criteria, dependencies, and size. The repository has none today.
- Label work by priority, area, kind, and tax year.
- Keep only one milestone "Now"; moving work forward requires an explicit tradeoff recorded in the Status table.
- Record completed outcomes in release notes rather than allowing this file to become a second changelog.
- This is the only plan of record. Do not start a second planning document; if a review produces one, fold its live findings in here and archive it under `docs/reviews/` with a date.
