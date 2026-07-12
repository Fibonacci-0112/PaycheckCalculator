# Wyoming (WY)

Wyoming does not levy a state individual income tax and has no employee-paid
state payroll assessments (state unemployment insurance is funded solely by
employers under Wyo. Stat. § 27-3-501 et seq.), so state withholding is
always zero.

Wyoming withholding is handled by the dedicated `WyomingWithholdingCalculator`
registered in `PaycheckCalculator.App/MauiProgram.cs` and `PaycheckCalculator.Blazor/Program.cs`.
The calculator exposes an empty input schema and always returns zero withholding
with the description "No state income tax".

Keeping Wyoming in its own module (instead of the generic
`NoIncomeTaxWithholdingAdapter`) aligns it with the per-state plugin model and
leaves a natural home for any future state-specific payroll contribution.

