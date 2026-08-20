# AGENTS.md - PaycheckCalculator.App

Scope: this file applies to everything under `PaycheckCalculator.App/`.

## Role of this project

`PaycheckCalculator.App` is the .NET MAUI client for Android, iOS, macOS (Mac Catalyst), and Windows. It presents the calculator, results, saved paychecks, budget features, account/sync screens, export, print, and local offline storage. The calculation truth lives in Core; this project adapts that truth to a native UI.

## Architecture rules

- Follow MVVM. Keep state and commands in `ViewModels/`, UI-only shapes in `Models/`, and domain-to-UI translation in `Mappers/`.
- Use CommunityToolkit.Mvvm source generators for observable state and commands where consistent with existing code.
- Keep XAML pages and code-behind thin. Do not place tax math, deduction math, budget math, or merge logic in views, converters, drawables, or behaviors.
- Route paycheck inputs through the existing mappers before calling Core services.
- Preserve schema-driven state fields through `StateFieldViewModel`; do not hardcode per-state controls when a schema can express the input.

## Platform and packaging rules

- Target Android, iOS, Mac Catalyst, and Windows unless the task explicitly changes platform support.
- Do not change `WindowsPackageType`, Android minimum version, target frameworks, application ID, or MAUI single-project settings casually.
- Tax data must remain packaged as `MauiAsset` entries with logical names matching the names expected by Core loaders, including `schemas/<state>.json`.
- App storage should keep saved paychecks usable offline. Account sync must be optional.

## Export and UI behavior

- Keep CSV/PDF/print rendering in `Services/` and presentation models. Services should persist/open rendered bytes; they should not recalculate taxes.
- Results, annual projections, and A/B comparisons should stay consistent with the Blazor experience unless a task explicitly asks for platform-specific behavior.
- Prefer accessible, predictable controls over custom drawing. Use custom drawables only for genuinely visual components such as charts.

## Useful commands

```bash
dotnet build PaycheckCalculator.App
dotnet build PaycheckCalculator.App -f net10.0-android
dotnet build PaycheckCalculator.App -f net10.0-ios
dotnet build PaycheckCalculator.App -f net10.0-maccatalyst
dotnet build PaycheckCalculator.App -f net10.0-windows10.0.19041.0
```

Building this project requires the MAUI workload; non-MAUI projects and tests should still build without it.
