# PaycheckCalc Wiki

Welcome to the **PaycheckCalc** wiki — the documentation home for the paycheck calculator.

PaycheckCalc computes net pay, tax withholdings, and deductions for all 50 US states plus DC using 2026 tax tables. It ships two front-ends — a **.NET MAUI** app (Android & Windows) and a **Blazor Server** web app — backed by the UI-agnostic `PaycheckCalc.Core` library. Saved paychecks can optionally sync between the two via a user account (see [Accounts & Sync](Accounts-and-Sync.md)).

---

## Table of Contents

### Getting Started

- **[Getting Started](Getting-Started.md)** — Prerequisites, how to build, test, and run the app.

### Architecture & Design

- **[Architecture](Architecture.md)** — Solution structure, MVVM pattern, dependency injection, and data flow.
- **[Tax Calculation Engine](Tax-Calculation-Engine.md)** — How gross pay, FICA, federal withholding, and state withholding are calculated.
- **[State Tax Coverage](State-Tax-Coverage.md)** — Full list of supported states, calculator categories, and how to add a new state.
- **[Accounts & Sync](Accounts-and-Sync.md)** — Optional accounts, local persistence, and how saved paychecks sync between the apps.

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
| Test Suite | [`PaycheckCalc.Tests/`](../../PaycheckCalc.Tests/) |

---

## Technology Stack

| Component | Technology |
|---|---|
| Framework | .NET 10 — MAUI |
| Target Platforms | Android, Windows 10+ |
| UI Pattern | MVVM with CommunityToolkit.Mvvm |
| Test Framework | xUnit 2.9.3 |
| Tax Data | JSON-based IRS 15-T and state tax bracket tables (2026) |
