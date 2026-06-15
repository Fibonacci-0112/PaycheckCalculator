# PaycheckCalc Wiki

Welcome to the **PaycheckCalc** wiki — the documentation home for the paycheck calculator.

PaycheckCalc computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. It ships two front-ends — a **.NET MAUI** app (Android & Windows) and a **Blazor Server** web app — backed by the UI-agnostic `PaycheckCalc.Core` library. It also includes a gross-up calculator, an annual projection, and a monthly budget tracker. Saved paychecks and budgets can optionally sync between the two front-ends via a user account (see [Accounts & Sync](Accounts-and-Sync.md)).

---

## Table of Contents

### Getting Started

- **[Getting Started](Getting-Started.md)** — Prerequisites, how to build, test, and run the app.

### Architecture & Design

- **[Architecture](Architecture.md)** — Solution structure, MVVM pattern, dependency injection, and data flow.
- **[Tax Calculation Engine](Tax-Calculation-Engine.md)** — How gross pay, FICA, federal withholding, and state withholding are calculated.
- **[State Tax Coverage](State-Tax-Coverage.md)** — Full list of supported states, calculator categories, and how to add a new state.
- **[Budgeting](Budgeting.md)** — The monthly budget tracker (50/30/20, categories, transactions, projection).
- **[Accounts & Sync](Accounts-and-Sync.md)** — Optional accounts, local persistence, and how saved paychecks and budgets sync between the apps.

### Using the App

- **[UI Guide](UI-Guide.md)** — App navigation, pages, input forms, and results.

### Development

- **[Contributing](Contributing.md)** — Development workflow, testing expectations, coding conventions, and guidelines for contributors.

---

## Quick Links

| Resource | Location |
|---|---|
| README | [`README.md`](../../README.md) |
| UML Class Diagram | [`docs/class-diagram.md`](../class-diagram.md) |
| Core Library | [`PaycheckCalc.Core/`](../../PaycheckCalc.Core/) |
| MAUI App | [`PaycheckCalc.App/`](../../PaycheckCalc.App/) |
| Blazor Web App | [`PaycheckCalc.Blazor/`](../../PaycheckCalc.Blazor/) |
| Shared (sync contracts) | [`PaycheckCalc.Shared/`](../../PaycheckCalc.Shared/) |
| Sync API | [`PaycheckCalc.Api/`](../../PaycheckCalc.Api/) |
| Test Suite | [`PaycheckCalc.Tests/`](../../PaycheckCalc.Tests/) |

---

## Technology Stack

| Component | Technology |
|---|---|
| Frameworks | .NET 11 — MAUI (app), ASP.NET Core Blazor Server (web), ASP.NET Core Web API (sync) |
| Target Platforms | Android, Windows 10+, web browser |
| UI Patterns | MVVM with CommunityToolkit.Mvvm (MAUI); interactive server-rendered Razor components (Blazor) |
| Accounts / Sync | ASP.NET Core Identity over EF Core PostgreSQL; shared last-write-wins merge |
| Test Framework | xUnit 2.9.3 |
| Tax Data | JSON-based IRS 15-T and state tax bracket tables (2026) |
