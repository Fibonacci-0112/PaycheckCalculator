# Utah (UT)

Utah state income tax withholding is currently computed by the generic
annualized percentage-method engine (`PercentageMethodWithholdingAdapter`
+ `PercentageMethodStateTaxCalculator`) using the `UsState.UT` entry in
`PaycheckCalculator.Core/Tax/State/StateTaxConfigs2026.cs`.

This folder exists as a placeholder so that any future Utah-specific
logic that cannot be expressed through `PercentageMethodConfig` (for
example, table-driven withholding, unique allowances, or a bespoke
`IStateWithholdingCalculator` implementation) can be added here alongside
the other state modules under `PaycheckCalculator.Core/Tax/`.
