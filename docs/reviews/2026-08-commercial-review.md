# Improvement Recommendations

_Prepared August 2026, against commit `788337b`. Companion to [ROADMAP.md](ROADMAP.md), which stays the engineering plan of record; this document is a commercial reprioritization of it._

## The one-paragraph version

This repository contains an unusually good calculation engine — 51 jurisdictions, 1,388 test cases, source-cited results, a "Show Your Work" tree, gross-up, supplemental wages, and self-employment — and essentially **no revenue plumbing**. There is no way for a willing customer to pay, no measurement of who visits or what they do, and the web app's architecture makes the cheapest customer-acquisition channel (organic search) the most expensive thing to serve. Accuracy is already past the point of commercial diminishing returns; distribution, measurement, and a purchase path are not started. The recommended sequence is **instrument → acquire → charge**, and most of it is small work, because the engine already computes far more than the UI exposes.

---

## Part 1 — Revenue blockers (fix these first)

### 1.1 There is no way to give you money

`FreeEntitlementProvider.IsPro` is a hardcoded `false` (`PaycheckCalculator.Shared/Entitlements/FreeEntitlementProvider.cs:9`), registered in both front-ends (`PaycheckCalculator.Blazor/Program.cs:23`, `PaycheckCalculator.App/MauiProgram.cs:59`). The only paywall in the product is `PaycheckCalculator.Blazor/Components/Pages/Budget.razor:375-381`, and it is a **dead end**: it renders the words "Pro feature" and a description, with no price, no button, no waitlist, no link. A user who has decided to pay cannot.

Recommended, in order:

1. **Today (hours, not weeks):** turn the dead-end gate into an email capture — "Pro launches soon, $X/yr — get notified." This costs almost nothing and immediately produces the only two numbers that matter: how many people hit the gate, and how many want it enough to leave an address. Run the same treatment at 2-3 price points.
2. **Then:** Stripe Checkout + webhook against `PaycheckCalculator.API`, with a `subscriptions` table keyed to the existing `IdentityUser`. `IEntitlementProvider` is already the correct seam — swap the implementation, don't redesign.
3. **Non-negotiable:** entitlement is currently **presentation-only**. `IsPro` gates markup, not data. The moment a paid tier exists, every Pro-only computation must be enforced server-side in the API; a client flag is not a paywall.

### 1.2 Nothing is measured

There is no analytics, telemetry, or event logging anywhere in the solution — a search across both front-ends for `gtag|analytics|plausible|posthog|telemetry` returns nothing but unrelated comment text. You cannot currently answer: how many people visit, which state pages draw them, what fraction complete a calculation, where they abandon, or whether anyone ever reaches the Pro gate.

Recommend a privacy-preserving funnel before any feature work: `landed → calculated → saved → signed up → hit gate → converted`, segmented by landing page and state. The roadmap's rule (Milestone 5) is right and should be adopted now, not later: **never log payroll values by default** — event names and coarse dimensions only. Given the product's privacy positioning, self-hosted event capture in the existing API is a better fit than a third-party pixel, and avoids the cookie-consent burden that would otherwise land on every page.

### 1.3 Legal pages will not survive a payments or store review

`Privacy.razor` is three sentences; `Terms.razor` is three sentences. That is below the bar for Stripe onboarding, ad-network approval, Apple/Google review, and CCPA/CPRA. Before charging anyone you need: data categories collected, retention periods, sub-processors, deletion mechanics, cookie/advertising disclosure, children's-data statement, contact address, and an effective date. This is a half-day of writing that otherwise blocks every revenue path simultaneously.

---

## Part 2 — Acquisition: the SEO foundation is started, and under-built

Someone has already done the right groundwork — 51 state landing pages (`StateLandingPage.razor`), a generated sitemap (`Program.cs:52-63`), canonical tags, OpenGraph, and JSON-LD. Four things hold it back.

### 2.1 A one-line bug is hiding your sitemap

`PaycheckCalculator.Blazor/wwwroot/robots.txt` ends with:

```
Sitemap: /sitemap.xml
```

The sitemaps protocol requires this directive to be a **fully-qualified absolute URL**; crawlers are not obliged to resolve a relative path here, and Google's documentation is explicit about it. The sitemap is currently generated correctly and then advertised in a form search engines may ignore. Because the host isn't known at build time, serve `robots.txt` from a minimal-API endpoint next to the sitemap and interpolate `ctx.Request.Scheme`/`Host`, exactly as the sitemap route already does.

While there: `Home.razor:6` uses a relative canonical (`href="/"`) while `StateLandingPage.razor:10` uses an absolute one. Make both absolute.

### 2.2 The state pages are thin, and thin pages at scale look like doorway pages

Each of the 51 pages is ~5 lines of unique prose from `StateMetadata.cs` sitting on top of an identical 1,732-line calculator. That is a recognizable pattern to Google's helpful-content systems: 51 near-duplicate URLs differentiated by a paragraph. The pages may rank thinly or not at all, and at worst drag sitewide quality.

Each state page should carry material a competitor would have to work to copy — and you already have the data to generate most of it:

- The state's actual bracket/rate table for 2026, rendered from the tax JSON you already ship.
- The state's disability/paid-leave line where one exists (CA SDI, CO FAMLI, CT PFMLI, WA Cares), which most competitors get wrong or omit.
- A worked take-home table at $40k/$60k/$80k/$100k/$150k for that state — generated by the engine at build time, so it is always correct and always unique.
- Reciprocity and local-tax caveats where they apply.
- 5-8 state-specific FAQs with `FAQPage` structured data (currently only `WebApplication` schema is emitted).

### 2.3 The highest-volume query families have no pages — and the engine already answers them

This is the single largest missed opportunity in the repository. `Pay/HourlySalaryCalculator.cs` is **fully implemented, DI-registered, and covered by its own test file — and referenced by neither front-end.** A search for `HourlySalary|PayConversionMode` across `PaycheckCalculator.App` and `PaycheckCalculator.Blazor` returns zero hits. Finished, tested inventory is sitting invisible.

Page families to build, all backed by engines that already exist:

| Page family | Engine | Status |
|---|---|---|
| "$75,000 a year is how much an hour" | `HourlySalaryCalculator` | **Built, unsurfaced** |
| "$X salary after taxes in \<state\>" (51 × N salary points) | `PayCalculator` | Built |
| "Bonus tax calculator" / "bonus after taxes" | `BonusCalculator` | Built, surfaced |
| "1099 / self-employment tax calculator" | `SelfEmploymentCalculator` | Built, surfaced |
| "Gross-up calculator" | `GrossUpCalculator` | Built, surfaced |
| "Overtime pay calculator" | `PayCalculator` | Built |

Each is a prefilled deep link plus unique generated copy. Generated at build time from the engine, a few thousand genuinely-correct, genuinely-distinct pages are reachable — and correctness is the one axis where this codebase beats every incumbent.

### 2.4 No shareable URLs

Calculator state lives entirely in the Blazor circuit. There is no `?state=ca&salary=75000&frequency=biweekly` representation, so: results can't be shared or bookmarked, paid ads can't land on pre-filled intent, "email me this result" is impossible, and §2.3's page families have nothing to link into. Deep-link parameters are a small change that unblocks sharing, paid acquisition testing, and the entire long-tail strategy at once.

---

## Part 3 — Architecture choices with direct P&L consequences

### 3.1 Blazor Server is the wrong hosting model for ad/SEO traffic

`App.razor:14` applies `@rendermode="InteractiveServer"` to the whole router, and `Program.cs:66` registers only the interactive-server render mode. Every visitor — including the bounce traffic that dominates SEO-driven calculator sites — opens and holds a SignalR circuit backed by server memory. Consequences:

- **Cost scales with concurrent visitors, not with usage.** This is the most expensive way to serve traffic whose per-visit revenue is fractions of a cent.
- **Interaction latency depends on a live socket round-trip.** Most paycheck-calculator traffic is mobile, often on poor connections; this is exactly the profile that suffers worst on INP, a ranking signal.
- **A dropped connection breaks the page**, and with it any unsaved work.

Recommendation: static SSR for landing and content, with interactivity as islands — `InteractiveAuto`/WebAssembly for the calculator, or a stateless server-rendered form post. Circuits should be reserved for signed-in workflows where the cost is justified by a logged-in user. This is a significant refactor and should be sized honestly, but it determines the unit economics of every acquisition channel.

### 3.2 The web app forgets everything when the tab closes

`SessionPaycheckStore` and `SessionBudgetStore` are circuit-scoped by design (`Program.cs:18-31`). For the calculator that is defensible. For the **budget tracker** it is fatal: a budget tool that forgets your categories, bills, and goals the moment you close the tab cannot build the habit that justifies a subscription. The roadmap files browser persistence and PWA at P2; for a revenue goal it belongs near the front, because recurring-revenue products are sold on retention and this is the retention surface.

Recommend `localStorage`-backed persistence for anonymous web users plus an installable PWA, with the account upgrade path positioned as "keep this across devices" — which is also a natural, non-coercive reason to create an account, feeding §1.1.

### 3.3 Tax-year coupling is both the biggest technical risk and the subscription mechanic

`TaxYearSupport` supports exactly 2026 and fails closed on anything else (`PayCalculator.cs:31-33`) — the fail-closed behavior is correct and worth keeping. But the entire product expires annually. The roadmap's Milestone 1 (versioned tax packs) is correctly identified as the highest-leverage architectural change; commercially it is also **the renewal mechanic**. Multi-year support is what makes an annual subscription make sense to a customer rather than feel like rent.

---

## Part 4 — What to actually sell

Free calculators are abundant. Nothing currently gated would make someone pay, because budget reports are a commodity. Ranked by willingness-to-pay against build cost:

1. **Paycheck audit — "why is my check short?"** The highest-intent moment in this entire category: someone with a stub in hand who thinks they've been shorted. The engine already produces a fully itemized expected paycheck with per-line explanations; the missing piece is an input form for actual stub values and a diff view highlighting the line that disagrees. This is the roadmap's P1 and I'd rank it first commercially too — it converts anxiety into a purchase, and the explanation tree is the product.
2. **Complete 1099 / contractor planner.** Contractors have real money at stake, a recurring quarterly deadline, and an established habit of paying $50-150/yr for tools in this class. `SelfEmploymentCalculator` already does SE tax and the 1040-ES schedule; the gap is federal income tax on the annual side. Best revenue-per-unit-of-work in the repository.
3. **Multi-year history and unlimited saved paychecks.** Near-zero build — it is an entitlement check over data you already store and sync. Classic, uncontroversial freemium line.
4. **W-4 optimizer.** Strong pull ("how do I stop owing in April"), but genuinely blocked on an annual liability engine. Don't ship a version that optimizes one withholding formula against itself — the roadmap's warning here is correct.
5. **Budget reports.** Already built, already gated. Keep as a bundle sweetener, not the headline.

**Hold the line the roadmap draws:** current-year accuracy, source citations, and the explanation tree must stay free. They are the acquisition engine and the trust asset; paywalling them would kill the channel that feeds everything above.

### 4.1 The under-considered option: sell the engine, not the app

A deliberate divergence from the roadmap, which defers any public API to "later, after everything is mature."

The genuinely rare asset here is not the UI — it is a **UI-agnostic, source-cited, 51-jurisdiction withholding engine with 1,388 tests behind it.** Payroll startups, HR platforms, staffing and contractor marketplaces, and fintechs all need exactly this and overwhelmingly build it badly or license it expensively. A metered calculation API or licensed SDK carries far higher revenue per customer than consumer subscriptions, needs no ad inventory, no SEO, and no app-store cut — and it is the one thing a competitor cannot clone in a weekend.

The roadmap's caution is reasonable (auth, quotas, compatibility policy, and support obligations are real), and B2B does demand a support commitment consumer traffic doesn't. But "after Milestones 0-5" likely defers the highest-margin revenue in the repository by a year or more. I'd recommend at minimum validating demand now — a landing page and ten customer conversations — before deciding the sequencing. If B2B demand is real, it should move up, not wait.

### 4.2 A note on the MAUI app

Consumer app-store revenue for utility calculators is historically weak, and `PaycheckCalculator.App` costs real maintenance across four platforms. It is also not yet store-ready: `ApplicationId` is `com.erik.paycheckcalc` with `ApplicationDisplayVersion 1.0`/`ApplicationVersion 1` hardcoded (`PaycheckCalculator.App.csproj:14-16`), and MAUI isn't built in CI at all. Recommend treating mobile as a **retention and subscription-delivery surface for existing users** rather than an acquisition channel, and putting marginal effort into web. If mobile is meant to carry revenue, it needs a real bundle identifier, build-supplied versioning, store listings, and CI coverage first.

---

## Part 5 — Correctness and infrastructure that gate the above

These are already on the roadmap; noting only where the revenue goal changes their urgency.

- **API is not production-capable and will be the payment-integrity surface.** `PaycheckCalculator.API/Program.cs` falls back to the local development credentials (`Username=admin;Password=password`) when no connection string is configured — a misconfigured deploy starts successfully on default credentials rather than failing loudly. Migrations run unconditionally at startup (`Program.cs:25-32`), and there is no rate limiting, health endpoint, email verification, or structured logging. Roadmap Milestone 3 covers all of it; the day you take money, this stops being technical debt and becomes financial risk.
- **Accessibility is a legal exposure, not just a quality item.** Consumer-finance sites are frequent ADA web-accessibility targets in the US. Roadmap Milestone 5's WCAG 2.2 AA goal is right; for a revenue-generating US finance site it should move earlier.
- **CI covers one project.** `.github/workflows/dotnet.yml` builds and tests only `PaycheckCalculator.Tests` (Core/Shared/API/Blazor come along transitively); MAUI is never built. Any shipped mobile revenue depends on a runtime CI does not exercise.
- **Documentation drift, already partially corrected.** The solution moved to .NET 10 LTS (`global.json` pins `10.0.400`; every project targets `net10.0`), but `CLAUDE.md:15,46,48` and `AGENTS.md:11,42,44` still describe `net11.0` and an SDK pin of `11.0.100-preview.7.26381.103`, and `ROADMAP.md` still lists "move off .NET 11 previews" as open debt in Milestone 2.1 and its debt register. The agent-instruction drift is corrected in this change; the roadmap text is the owner's call.

---

## Suggested sequence

**Weeks 1-2 — make the business observable and chargeable.**
Analytics funnel · email capture replacing the dead-end Pro gate · absolute-URL `robots.txt` · absolute canonicals · real Privacy/Terms.

**Weeks 3-6 — turn the engine into traffic.**
Surface `HourlySalaryCalculator` · deep-link URLs · generated salary/hourly page families · deepen the 51 state pages with generated tables and FAQ schema.

**Weeks 7-12 — build something worth buying.**
Paycheck audit · browser-persistent budget + PWA · Stripe Checkout with server-enforced entitlements · API hardening as its prerequisite.

**In parallel, cheap:** ten B2B conversations to price the engine as a licensed API (§4.1). It may reorder everything above.

**Then:** versioned tax packs (roadmap Milestone 1) — required before the 2027 season regardless of which revenue path wins.

---

## Verification notes

Every file, line number, and code claim above was read directly from the working tree at `788337b`. No .NET SDK is available in the environment where this review was written, so **the solution was not built and the test suite was not executed**; the 1,388 figure is a count of `[Fact]`/`[Theory]` attributes across 83 test files, not a passing-run result. Traffic, ranking, and willingness-to-pay claims are judgments about the category, not measurements of this product — which is precisely why §1.2 is ranked where it is.
