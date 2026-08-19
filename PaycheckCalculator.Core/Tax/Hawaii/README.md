# Hawaii (HI)

Hawaii state income tax withholding is computed by the dedicated
[`HawaiiWithholdingCalculator`](HawaiiWithholdingCalculator.cs), which
implements the annualized percentage method from the Hawaii Department of
Taxation publication *Booklet A, Employer's Tax Guide* (Appendix —
Percentage Method Tables for Computing Hawaii Withholding Tax).

The calculator exposes a schema-driven `IStateWithholdingCalculator`
plugin with:

- **HW-4 filing status** — `Single` (used for Single, Head of Household,
  and Married Filing Separately) and `Married` (Married Filing Jointly).
- **HW-4 allowances** — `$1,144` annual exemption per allowance claimed
  on Form HW-4, subtracted from annual taxable wages after the
  filing-status standard deduction (`$2,200` single / `$4,400` married).
- **Additional withholding** — per-period extra withholding requested on
  Form HW-4, added after the percentage-method computation.

The 2026 graduated brackets (1.4%–11.0%) and the per-filing-status
standard deductions are hard-coded as `decimal` constants in the
calculator. Registration is centralized in `MauiProgram`,
`PaycheckCalculator.Blazor/Program.cs`, and `PaycheckCalculator.API/Program.cs`,
alongside the other dedicated state calculators.

