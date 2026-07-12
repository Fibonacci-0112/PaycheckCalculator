# Tennessee (TN)

Tennessee does not levy a state individual income tax, so withholding for this
state is currently handled by the generic
`NoIncomeTaxWithholdingAdapter` registered in
`PaycheckCalculator.App/MauiProgram.cs`. No state-specific calculator lives in
this folder.

This folder exists as a placeholder so that any future state-specific
logic (for example, a state disability/paid-leave contribution) can be
added here alongside the other state modules under
`PaycheckCalculator.Core/Tax/`.
