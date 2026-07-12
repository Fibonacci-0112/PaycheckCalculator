# Iowa (IA)

Iowa state income tax withholding is handled by the dedicated
`IowaWithholdingCalculator` registered through `StateCalculatorRegistry`.

The calculator applies a flat 3.65% rate to state taxable wages
(gross wages minus pre-tax deductions that reduce state wages),
annualized by pay frequency and divided back to the pay period.
Any extra per-pay amount entered on IA W-4 Line 6 is added on top.

This replaces the previous `UsState.IA` entry in
`PaycheckCalculator.Core/Tax/State/StateTaxConfigs2026.cs`, which relied on
the generic annualized percentage-method engine
(`PercentageMethodWithholdingAdapter` +
`PercentageMethodStateTaxCalculator`).  The dedicated calculator keeps
Iowa's logic alongside the other state modules so future Iowa-specific
behavior — for example, schema-backed allowances, a revised flat rate,
or table-driven withholding — can be added here without perturbing the
shared percentage-method configs.

