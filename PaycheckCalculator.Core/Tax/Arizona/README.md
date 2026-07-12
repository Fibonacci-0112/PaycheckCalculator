# Arizona (AZ)

Arizona state income tax withholding is computed by
`ArizonaWithholdingCalculator`, a dedicated `IStateWithholdingCalculator`
plugin in this folder.

Unlike most states, Arizona does not use an annualized percentage
method with allowances and standard deductions.  Instead, Form A-4
("Employee's Arizona Withholding Election") asks the employee to pick
one of seven flat rates — **0.5%, 1.0%, 1.5%, 2.0%, 2.5%, 3.0%, or
3.5%** — which the employer applies directly to gross taxable wages each
pay period.  When an employee has not filed a valid A-4, the employer
must default to **2.0%** per the Arizona Department of Revenue.

The calculator exposes two schema fields:

- `WithholdingRate` — picker of the seven A-4 percentages (default `2.0%`).
- `AdditionalWithholding` — optional extra dollars per pay period from
  Form A-4, Line 2.

Pre-tax deductions that reduce state wages are subtracted from gross
before the flat rate is applied; the result is rounded to cents
(away-from-zero) and the extra withholding is then added.

Because the per-period calculation is a pure percentage election, the
calculator does not live in `StateTaxConfigs2026.cs`; the `UsState.AZ`
entry there has been removed and the dedicated calculator is registered
explicitly alongside the other dedicated state modules.
