# Ohio (OH)

Ohio state income tax withholding is implemented by the dedicated
`OhioWithholdingCalculator` in this folder.  It follows the Ohio
Department of Taxation 2026 "Optional Computer Formula" (annualized
percentage method) and consumes Form IT-4 exemption counts supplied
through the state input schema.

Key facts:

* Uses the **IT-4 exemption allowance** of **$650 per exemption**
  (annualized) to reduce annual wages before applying the bracket
  table.  Exemptions on IT-4 are one for the employee, one for a
  spouse, and one for each dependent.
* Does **not** branch on filing status — Ohio's withholding tables are
  uniform across filers.
* Applies the 2026 bracket schedule:
  * 0% on annual taxable income up to $26,050
  * 2.75% on annual taxable income over $26,050
* Supports an additional per-period withholding amount.

Because Ohio has a dedicated calculator, the `UsState.OH` entry has
been removed from `PaycheckCalculator.Core/Tax/State/StateTaxConfigs2026.cs`
and the calculator is registered explicitly in `MauiProgram` and the
Blazor `Program` DI setup.
