---
applyTo: "PaycheckCalculator.Core/Data/**/*.json"
---

# Tax data instructions

- Treat JSON tax tables as authoritative structured data, not as free-form content.
- Preserve existing key names and shapes unless the consuming C# models and loaders are intentionally being updated together.
- Do not rename tax data files casually; project files, DI setup, and tests depend on stable names.
- Keep data changes traceable to the applicable tax year and source publication.
- When editing data, check the matching calculator and add or update regression tests that prove the new values are consumed correctly.
