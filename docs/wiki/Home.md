# PaycheckCalc Wiki

Welcome to the **PaycheckCalc** wiki — the documentation home for the paycheck calculator.

PaycheckCalc computes gross pay, tax withholding, deductions, employee-paid state disability / paid-leave premiums, and net pay for all 50 US states plus the District of Columbia using 2026 tax data. It includes a .NET MAUI app, a Blazor Server web app, a shared Core calculation engine, shared sync/contracts code, and an optional ASP.NET Core Web API for accounts and sync.

Current major capabilities include standard paycheck calculation, gross-up calculation, annual projection, saved paycheck comparison, CSV/PDF/print export, a monthly budget tracker, recurring bills, savings goals, and Pro-gated budget reporting infrastructure.

---

## Table of Contents

### Getting Started

- **[Getting Started](Getting-Started.md)** — Prerequisites, solution layout, build/test/run commands, project dependencies, and tax-data wiring.

### Architecture & Design

- **[Architecture](Architecture.md)** — Solution structure, project boundaries, dependency injection, front-end data flow, budget/report architecture, and sync layering.
- **[Tax Calculation Engine](Tax-Calculation-Engine.md)** — Gross pay, deductions, FICA, federal withholding, state withholding, rounding, gross-up, and annual projection.
- **[State Tax Coverage](State-Tax-Coverage.md)** — Supported states, calculator categories, dynamic schema model, disability / paid-leave premiums, and state-update workflow.
- **[Budgeting](Budgeting.md)** — Budget methods, categories, transactions, recurring bills, savings goals, reports, and sync.
- **[Accounts & Sync](Accounts-and-Sync.md)** — Optional accounts, local stores, API endpoints, PostgreSQL persistence, and last-write-wins merge rules.

### Using the App

- **[UI Guide](UI-Guide.md)** — MAUI tab structure, Blazor pages, input forms, results, exports, budgeting, accounts, and report gating.

### Development

- **[Contributing](Contributing.md)** — Development workflow, testing expectations, code organization, tax logic rules, sync rules, and documentation expectations.

---

## Quick Links

| Resource | Location |
|---|---|
| README | [`README.md`](../../README.md) |
| UML Class Diagram | [`docs/class-diagram.md`](../class-diagram.md) |
| Solution file | [`PaycheckCalculator.slnx`](../../PaycheckCalculator.slnx) |
| Core Library | [`PaycheckCalculator.Core/`](../../PaycheckCalculator.Core/) |
| MAUI App | [`PaycheckCalculator.App/`](../../PaycheckCalculator.App/) |
| Blazor Web App | [`PaycheckCalculator.Blazor/`](../../PaycheckCalculator.Blazor/) |
| Shared contracts / sync | [`PaycheckCalculator.Shared/`](../../PaycheckCalculator.Shared/) |
| Sync API | [`PaycheckCalculator.Api/`](../../PaycheckCalculator.Api/) |
| Test Suite | [`PaycheckCalculator.Tests/`](../../PaycheckCalculator.Tests/) |

---

## Technology Stack

| Component | Technology |
|---|---|
| Primary SDK | .NET 11 preview pinned in `global.json` |
| Core | UI-agnostic `net11.0;net9.0` library when built with the .NET 11 SDK; `net9.0` fallback on older SDKs |
| MAUI App | .NET MAUI, Android, Windows 10+, CommunityToolkit.Mvvm |
| Web App | ASP.NET Core Blazor Server |
| Sync API | ASP.NET Core minimal APIs, ASP.NET Core Identity, EF Core, Npgsql/PostgreSQL |
| Shared Layer | DTOs, JSON converters, deterministic mergers, API client, store abstractions, entitlements |
| Tests | xUnit |
| Tax Data | JSON-based IRS 15-T and state tax tables plus one dynamic-input schema JSON per state / DC |
