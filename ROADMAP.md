# PaycheckCalculator Roadmap

_Last updated: September 10, 2026._

This is the living product and engineering roadmap for PaycheckCalculator, and the **single plan of
record**. It describes intended direction, not a promise of dates. Milestones are ordered by
dependency and risk; accuracy, explainability, and data safety take priority over feature count.

## Scope

**This is a personal project, built for its own sake and for the author's own use.** It is not
pursuing users, revenue, or a market. That decision is what orders everything below, so it is stated
first rather than left implicit.

Concretely, it means the work worth doing is the work that makes the thing *correct*, *pleasant to
own*, and *still working next year* — not the work that would make it sell. Milestones aimed at
acquisition, measurement, and monetization are **parked**: kept in the document with their analysis
intact, so they can be revived if the goal ever changes, but explicitly not scheduled and not
blocking anything.

A commercial review from August 2026 is archived at
[`docs/reviews/2026-08-commercial-review.md`](docs/reviews/2026-08-commercial-review.md). Most of it
argues for a goal this project does not have. It is retained because its *technical* findings — the
hosting model, the `robots.txt` bug, the entitlement seam — are sound regardless of motive, and
because reversing the scope decision should start from a real analysis rather than a fresh guess.

## Status

| | Milestone | Why |
|---|---|---|
| **Now** | **1 — Tax-year platform** | The only item here with a real deadline. The engine is hardcoded to 2026 and fails closed on every other year, so it simply stops working for the 2027 season unless this lands. Everything else can wait; this cannot. |
| **Next** | 2 — Craft and maintainability | The actual point of a project like this. CI coverage, the ~400 unenforced formatting violations, and the two thousand-line orchestration files are the things that make it pleasant or unpleasant to come back to. |
| **Then** | 3 — Run it somewhere · 6 — Features worth having | Deploy it so it can be used from a phone without a local SDK, then build the handful of features the author would actually use. |
| **Background** | 0 — Trust baseline · 4 — Sync reliability | Standing obligations rather than scheduled work. 4 matters only to the degree accounts get used across devices. |
| **Parked** | 5 — Revenue · most of 3 · parts of 7 | Out of scope under the goal above. Kept, not scheduled. |

Only one milestone is "Now" at a time. Moving work forward out of order requires an explicit tradeoff,
recorded here.

## Product direction

PaycheckCalculator aims to be a genuinely correct, genuinely explainable US paycheck calculator —
because that is a satisfying thing to build and a useful thing to own, not because it is trying to win
a market. The standard it holds itself to is the same either way; only the reason for holding it
differs.

The qualities below are the point of the project, not a positioning statement, and they hold whether
or not anyone else ever uses it:

- **Accuracy-first** — every supported calculation is traceable to an authoritative rule or table and protected by explicit test cases.
- **Explainable** — every result can be traced step by step to the rule that produced it. This is the most interesting part of the codebase and the reason the engine is worth having.
- **Private by default** — core calculations work without an account, and local/offline use remains a first-class path.
- **Cross-platform, deliberately** — MAUI and web share one calculation engine. Parity is a choice to re-examine per feature (see Milestone 7), not an obligation.
- **Honest about scope** — estimates, unsupported local taxes, and assumptions are disclosed instead of hidden behind a precise-looking number.

Nothing here is or will be gated behind payment; see Milestone 5, which is parked.

## Current baseline

Verified against `b000521` on September 10, 2026:

- Federal withholding, FICA, and state withholding for **51 jurisdictions** (50 states + DC), each with a dedicated calculator and input schema.
- Six calculation pipelines: standard, gross-up, supplemental/bonus, self-employment, hourly↔salary conversion, and annual projection.
- A **119-rule source manifest** citing the official 2026 publication behind each number, validated at startup.
- **1,613 tests, all passing**, in ~4 seconds. The test project (27.7k lines) is larger than Core (15.7k).
- Clean build: 0 warnings, 0 errors across Core, Shared, API, Blazor and tests. The MAUI Android target builds with 5 known warnings.
- MAUI clients for Android, iOS, Mac Catalyst and Windows; a Blazor Server web app; an optional account/sync API.
- Saved-paycheck comparison, CSV/PDF export, budgeting, recurring bills, savings goals, and reports.

**Nothing is deployed.** There is no public instance and no users, and under the scope above that is
a fact to work with rather than a problem to solve. It means Milestone 3 shrinks to "somewhere to run
it", and it means every milestone premised on traffic is parked.

The repository has **no open GitHub issues and has never had any**. Milestone work should be converted
into focused issues before implementation begins — a document is not a work queue, and its absence is
a large part of why the next step is hard to see from a cold start.

## Delivery map

| Order | Milestone | Primary outcome | Status |
|---|---|---|---|
| 0 | Trust baseline | Correctness and source provenance stay measurable | Largely complete; standing obligations |
| **1** | **Tax-year platform** | Replace the single-year design with versioned tax data | **Now** — 2027 deadline |
| 2 | Craft and maintainability | CI coverage, enforced formatting, no thousand-line orchestrators | Next |
| 3 | Run it somewhere | Deployed and usable from a browser without a local SDK | Then — 3.1 and 3.3 only |
| 4 | Sync reliability | Accounts and multi-device sync that do not lose data | Background; only if used |
| 5 | Revenue | — | **Parked** — out of scope |
| 6 | Features worth having | The handful the author would actually use | Then — pick individually |
| 7 | Accessibility and platform | WCAG 2.2 AA on the web; settle the MAUI question | Later; Pro/billing items parked |

There are no time estimates. This is unpaid work with no deadline except the tax year, and inventing
week counts for a hobby project would only manufacture guilt.

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

### 0.3 Documentation reconciliation — **swept**

These baselines are now stated consistently across `README.md`, `CLAUDE.md`, the root and six
per-project `AGENTS.md` files, `docs/wiki/`, `docs/reference/`, and `replit.md`. They had regressed
to .NET 11 and a preview SDK pin everywhere except the root guidance files:

- Core targets **.NET 10** only; the SDK pin is `10.0.400` with `latestFeature` roll-forward, and no preview packages remain.
- The API uses Npgsql/PostgreSQL in normal operation; SQLite is limited to integration tests.
- CI runs two jobs: one restores, builds and tests `PaycheckCalculator.Tests` (building Core, Shared, API and Blazor transitively), and one builds the MAUI Android target. iOS, Mac Catalyst and Windows need other runners and stay uncovered.
- `README.md` lists all ten disability/paid-leave jurisdictions and Maryland county tax, not the four it previously named.

Deliberately left alone: `.agents/memory/dotnet-preview-sdk-setup.md` keeps its .NET 11 worked
example, since it documents a technique for any project pinning an SDK newer than the available
modules; it carries a note that it no longer describes this repository. The archived review under
`docs/reviews/` is frozen by design and is not swept.

Standing obligation: this is the second time these baselines have drifted. The durable fix is
mechanical — a CI check that greps the docs for the TFMs and SDK pin actually declared in
`global.json` and the csproj files — and belongs with the other CI work in 2.2.

### Exit criteria

- Every production calculator has a source record and verification owner/date. **Met.**
- All 51 jurisdictions pass registration, schema, and representative calculation tests. **Met.**
- Known documentation contradictions are resolved. **Met** — see 0.3. Staying met needs the automated check noted there.
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

## Milestone 2 — Craft and maintainability

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
- A documentation consistency check: assert the TFMs and SDK version named in `README.md`, `docs/` and the `AGENTS.md` files match `global.json` and the csproj files. This drift has now been repaired twice by hand (0.3).
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

## Milestone 3 — Run it somewhere

Scoped down to what a personal project needs: somewhere to use it from, and links that reproduce a
calculation. The acquisition and measurement sections below are parked.

### 3.1 Deploy something

- Stand up the Blazor app at a real domain, with TLS, a health check, and a documented redeploy path.
- Decide whether the sync API deploys alongside it or stays local-only for now. The calculator works fully without it.
- The Replit configuration is the closest thing to a deployment story and does not currently work: `.replit` declares a `dotnet-7.0` module that cannot satisfy the `10.0.400` pin, and `start-api.sh`/`start-blazor.sh` still point `DOTNET_ROOT` at a local `.dotnet/` directory nothing provisions. `replit.md` now documents this honestly rather than describing it as working. Either repair it or replace it with the real deployment target.

### 3.2 Hosting model — **parked**

The concern below is real but priced in visitor concurrency, which a personal instance does not have.
One user holding one SignalR circuit is fine. Revive this only if the app is ever opened to traffic.

<details>
<summary>Original analysis</summary>


`Components/App.razor` applies `@rendermode="InteractiveServer"` to the whole router, and `Program.cs`
registers only the interactive-server render mode. Every anonymous visitor therefore opens and holds a
SignalR circuit backed by server memory. For a calculator whose expected traffic is high-bounce and
mostly mobile, that means cost scales with concurrent visitors rather than usage, interaction latency
depends on a socket round-trip, and a dropped connection breaks the page.

- Evaluate static SSR for landing and content pages with interactivity as islands — `InteractiveAuto`/WebAssembly for the calculator, or a stateless server-rendered form post.
- Reserve circuits for signed-in workflows where the cost is justified.
- Size this honestly: it is a significant refactor, and it determines the unit economics of every acquisition channel. Deciding it after traffic arrives is more expensive than deciding it now.

</details>

### 3.3 Make results addressable — **keep**

Worth doing regardless of audience: a URL that reproduces a calculation is how you bookmark your own
scenarios, compare two of them side by side in tabs, or send one to someone. Small change, genuinely
useful to a single user.

Calculator state lives entirely in the circuit. There is no `?state=ca&salary=75000&frequency=biweekly`
representation, so a result cannot be bookmarked or reopened — every visit starts from an empty form.

- Add deep-link query parameters for the calculator's inputs, round-tripped through the URL.
- Pairs naturally with the tax-year identity work in 1.2: a shared link should pin the year it was calculated for, or it silently means something different next January.

### 3.4–3.6 Acquisition, measurement, and commercial legal pages — **parked**

All three exist to convert strangers into users and users into customers. None of that applies here.

Two exceptions worth doing anyway, because they are correctness bugs rather than growth work, and
they take minutes:

- `wwwroot/robots.txt` ends with `Sitemap: /sitemap.xml`. The sitemaps protocol requires an absolute URL. Wrong is wrong even when nobody is crawling.
- `Home.razor` uses a relative canonical (`href="/"`) while `StateLandingPage.razor` uses an absolute one. Make both absolute for consistency.

And one that changes shape rather than parking: if the app is ever deployed on a public URL, a short,
honest privacy note is worth writing — not for a payment processor, but because it is the decent thing
to put on a page that accepts salary figures. That is a paragraph, not the compliance document 3.6
describes.

<details>
<summary>Original analysis (3.4 SEO, 3.5 funnel, 3.6 commercial legal pages)</summary>

#### 3.4 Deepen the SEO surface that already exists

51 state landing pages, a generated sitemap, canonical tags, OpenGraph and JSON-LD are already in place.
Four things hold them back:

- `wwwroot/robots.txt` ends with `Sitemap: /sitemap.xml`. The sitemaps protocol requires an absolute URL; crawlers are not obliged to resolve a relative path. Serve `robots.txt` from a minimal-API endpoint and interpolate scheme/host exactly as the sitemap route already does.
- `Home.razor` uses a relative canonical (`href="/"`) while `StateLandingPage.razor` uses an absolute one. Make both absolute.
- Each state page is ~5 lines of unique prose over an identical calculator — a recognizable thin-page pattern at 51 URLs. Generate real per-state substance from data already shipped: the 2026 bracket table, the disability/paid-leave line where one exists, a worked take-home table at several salary points, and reciprocity/local-tax caveats.
- Emit `FAQPage` structured data alongside the existing `WebApplication` schema.

#### 3.5 Instrument the funnel

There is no analytics, telemetry or event logging anywhere in the solution. Before feature work resumes,
add a privacy-preserving funnel: `landed → calculated → saved → signed up → hit gate → converted`,
segmented by landing page and state.

- **Never log payroll values by default** — event names and coarse dimensions only.
- Given the product's privacy positioning, self-hosted event capture in the existing API is a better fit than a third-party pixel, and avoids a cookie-consent burden on every page.

#### 3.6 Legal pages that survive review

`Privacy.razor` and `Terms.razor` are 20 lines each, most of it markup. That is below the bar for
payment-processor onboarding, ad-network approval, and app-store review, and it blocks every revenue
path at once. Needed: data categories collected, retention periods, sub-processors, deletion mechanics,
cookie/advertising disclosure, children's-data statement, contact address, and an effective date.

</details>

### Exit criteria

- The calculator is reachable at a stable URL and gives a correct answer.
- A calculation can be shared as a link that reproduces it.
- Redeploying is one documented command, not an archaeology exercise.

## Milestone 4 — Sync reliability

Keep sync optional. Under the personal-project scope this is **background work, sized to actual use**:
if the account feature is never used across devices, most of it is unnecessary. Two items are worth
doing the moment the API runs anywhere but localhost, because they are about not getting burned rather
than about scale — the fallback credentials in 4.1 and the data-loss cases in 4.3. The rest can wait
indefinitely.

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

## Milestone 5 — Revenue — **PARKED**

Out of scope: this is a personal project and is not being sold. The section is kept intact so that
reversing that decision starts from analysis rather than a blank page, but nothing here is scheduled
and nothing else in this roadmap waits on it.

One consequence is worth acting on now, though, and it is the opposite of monetization: **the Pro gate
is currently getting in the author's own way.** `FreeEntitlementProvider.IsPro` is hardcoded `false`,
so `Budget.razor` hides the budget report views behind a "Pro feature" notice that leads nowhere — a
paywall with no product behind it, locking the author out of a feature that is already built and paid
for. Either delete the gate and the `IEntitlementProvider` indirection, or have the default provider
return `true`. Deleting is tidier; returning `true` keeps the seam if the scope decision ever reverses.

<details>
<summary>Original analysis, retained for a possible future change of scope</summary>

#### 5.1 A path from willing customer to paid

`FreeEntitlementProvider.IsPro` is a hardcoded `false`, registered in both front-ends. The only paywall
in the product renders the words "Pro feature" and a description, with no price, button, waitlist or
link. A user who has decided to pay cannot.

1. **Cheap first step:** turn the dead-end gate into an email capture — "Pro launches soon, $X/yr — get notified." It costs hours and produces the only two numbers that matter early: how many people reach the gate, and how many want it enough to leave an address. Run it at two or three price points.
2. **Then:** a checkout flow and webhook against `PaycheckCalculator.API`, with a subscriptions table keyed to the existing `IdentityUser`. `IEntitlementProvider` is already the right seam — swap the implementation, don't redesign it.
3. **Non-negotiable:** entitlement is currently presentation-only. `IsPro` gates markup, not data. The moment a paid tier exists, every Pro-only computation must be enforced server-side. A client flag is not a paywall.

#### 5.2 What to actually sell

Free calculators are abundant, and nothing currently gated would make someone pay. Ranked by
willingness-to-pay against build cost:

1. **Paycheck audit — "why is my check short?"** The highest-intent moment in the category: someone holding a stub who thinks they have been shorted. The engine already produces a fully itemized expected paycheck with per-line explanations; the missing piece is a form for actual stub values and a diff view highlighting the line that disagrees. Also the top-ranked feature in Milestone 6.
2. **Complete 1099 / contractor planner.** Real money at stake, a recurring quarterly deadline, and an established habit of paying for tools in this class. `SelfEmploymentCalculator` already does SE tax and the 1040-ES schedule; the gap is federal income tax on the annual side.
3. **Multi-year history and unlimited saved paychecks.** Near-zero build — an entitlement check over data already stored and synced. Enabled by Milestone 1.
4. **W-4 optimizer.** Strong pull, but genuinely blocked on an annual liability engine. Do not ship a version that optimizes one withholding formula against itself.
5. **Budget reports.** Already built and already gated. A bundle sweetener, not the headline.

**Hold the line:** current-year accuracy, source citations and the explanation tree stay free. They are
the acquisition engine and the trust asset.

#### 5.3 The other option — license the engine

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

#### Exit criteria (if ever revived)

- A customer can pay, and paying changes what the server returns, not just what the client renders.
- Entitlements are enforced in the API for every gated computation.
- Free-tier accuracy, citations and explanations are unchanged by the existence of a paid tier.

</details>

## Milestone 6 — Features worth having

Previously ranked by willingness-to-pay. Under the personal-project scope the only ranking that
matters is **which of these the author would actually use**, so treat the table as a menu rather than
a queue and pick individually. Nothing here is owed to anyone.

The P1 rows are still the strongest candidates on their merits: the paycheck audit turns the existing
explanation tree into an answer to "why is my check short?", and the 1099 planner and multi-state
support each close a real gap in what the engine can currently tell you.

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

- Meet WCAG 2.2 AA for the web experience and test keyboard, screen-reader, scaling, contrast, and error-state behavior on MAUI. The legal-exposure argument does not apply to a personal project, but the craft argument does, and keyboard and contrast work benefits the author too.
- **Decide the MAUI question.** Four platforms of maintenance currently carry no store presence: `ApplicationId` is `com.erik.paycheckcalc` and versions are hardcoded. Only Android is built by CI, so iOS, Mac Catalyst and Windows breakage is still invisible until someone builds locally. Under this scope, store listings are irrelevant — the real question is narrower: **which platforms does the author actually run it on?** Keep those, and drop or freeze the rest rather than paying parity costs on every feature for targets nobody launches. Currently undecided.
- ~~Enforce entitlements server-side, receipt validation, grace periods, refunds, restore-purchase flows.~~ **Parked** with Milestone 5.
- ~~Product analytics.~~ **Parked** — there is no funnel to learn about. If curiosity ever motivates local instrumentation, the rule still holds: never collect payroll values.

## Engineering debt register

Scored against the personal-project scope: several entries that read as debt for a commercial product
are simply not debt here, and say so rather than sitting on the list forever accruing guilt.

| Finding | Evidence at `b000521` | Planned response |
|---|---|---|
| Single-year coupling | `TaxYearSupport` supports only 2026; DI, filenames, explanations, and 1040-ES dates embed the year | Milestone 1 — **Now** |
| ~~Preview runtime graph~~ **Resolved** | `global.json` pins the .NET 10 GA SDK (10.0.400); all TFMs are `net10.0`; no preview packages | Remaining follow-on: central package management and lock files (2.1) |
| ~~MAUI never built by CI~~ **Resolved for Android** | The `android` job builds `net10.0-android`; the app had reached main broken at least once | Windows/macOS targets still uncovered (2.2) |
| Unenforced formatting | ~400 `dotnet format` whitespace violations across ~30 files; nothing checks it | One mechanical pass, then `--verify-no-changes` in CI (2.1) |
| UI orchestration size | `Calculator.razor` 1,948 lines; `CalculatorViewModel` 1,474; both grow per calculation mode | Capability-based refactor (2.3) |
| Repeated asset wiring | Tax JSON declared separately for MAUI, Blazor and tests, plus the DI loader | Manifest-driven MSBuild wiring (1.3) |
| ~~Documentation drift~~ **Resolved, twice now** | Had regressed to .NET 11 across six per-project `AGENTS.md`, three wiki pages, two reference chapters and `replit.md`; `README.md` under-reported state coverage | Swept (0.3). Recurrence is the real risk — add a docs-vs-`global.json` CI check (2.2) |
| Hosting model | `InteractiveServer` on the whole router; every visitor holds a SignalR circuit | **Not debt at this scope** — one user, one circuit. Parked (3.2); revisit only if opened to traffic |
| No measurement | No analytics, telemetry, or event logging anywhere in the solution | **Not debt at this scope** — nothing to measure and nobody to measure. Parked (3.5) |
| Pro gate blocks the author | `IsPro` hardcoded `false`, so `Budget.razor` hides already-built report views behind a dead-end notice | Inverted by the scope decision: not a missing purchase path but an unnecessary lock. Delete the gate or default `IsPro` to `true` (see Milestone 5 preamble) |
| Client-clock conflict resolution | Sync merge uses client `DateTimeOffset` values for last-write-wins and retains tombstones | Server revisions, delta sync, compaction (4.3) |
| API production gaps | Fallback database credentials (`admin`/`password`), startup migrations, no rate limiting or health endpoint | Only bites if the API leaves localhost — but then it bites hard, so fix the credential fallback before any remote deploy (4.1) |
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
- CI is green, and anything that changes a calculation says so plainly in the commit message.
- The change would still make sense to you six months later — the only reviewer this project reliably has.

## Success measures

Targets, not current readings. A short list, all of which are about the work being right rather than
about anyone noticing:

- Verified jurisdictions for the active tax year: **51 of 51**.
- Official/golden example pass rate: **100%**.
- Runtime build coverage in CI for every platform actually shipped: **100%**.
- Unresolved confirmed calculation defects: **0**.
- Web accessibility: **WCAG 2.2 AA** on critical flows.
- Time from picking the repo up after a month away to a green build: **minutes**. This is the measure a personal project lives or dies by, and it is what Milestone 2 is really for.

Removed with the commercial scope: conversion, traffic, and any target framed around a launch or a
service level owed to users.

## Deliberately not now

These add major legal, security, or operational scope and should not distract from the milestones above:

- Filing tax returns or acting as a payroll processor.
- Direct bank-account aggregation.
- Employer payroll administration and remittance.
- AI-generated tax advice.
- Nationwide local-tax claims without a maintainable authoritative data pipeline.
- Organization/team accounts before single-user sync is production-ready.

And, following from the scope decision at the top — not because they are bad ideas, but because they
serve a goal this project does not have:

- Charging anyone for anything, and the billing, entitlement enforcement, and commercial legal pages that would require.
- SEO depth, generated landing-page families, and structured data aimed at search traffic.
- Funnel analytics and conversion measurement.
- Licensing the engine commercially.

None of these are ruled out forever. They are ruled out *now*, by choice, and the analysis for each
survives in Milestone 5 and the archived review if that choice is ever revisited.

## Roadmap maintenance

- Review this document after every tax-data correction, and whenever picking the project back up after a gap. Monthly review is a process for teams; here it would just be a recurring chore to feel bad about skipping.
- Convert the active milestone into GitHub issues with acceptance criteria and dependencies. The repository has none today, and a 400-line document is not a work queue. Skip "owner" and "size" — there is one person and no sprint.
- Label work by priority, area, kind, and tax year.
- Keep only one milestone "Now"; moving work forward requires an explicit tradeoff recorded in the Status table.
- Record completed outcomes in release notes or commit messages rather than letting this file become a second changelog.
- If the scope statement at the top ever changes, re-read the parked sections and the archived review before rescheduling anything — they hold the reasoning, and re-deriving it from scratch would waste the analysis already done.
- This is the only plan of record. Do not start a second planning document; if a review produces one, fold its live findings in here and archive it under `docs/reviews/` with a date.
