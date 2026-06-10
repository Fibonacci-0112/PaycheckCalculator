# Indiana (IN)

Indiana state income tax withholding is implemented by
`IndianaWithholdingCalculator` — a dedicated
`IStateWithholdingCalculator` module that models the Form WH-4 exemption
structure directly instead of going through the generic
`PercentageMethodWithholdingAdapter`.

Characteristics:

- Flat 3.05% state adjusted gross income tax rate.
- Two independent WH-4 exemption inputs:
  - `Exemptions` — personal / spouse / age 65+ / blind exemptions,
    worth **$1,000 each** annually (WH-4 Lines 1–2).
  - `DependentExemptions` — additional dependent exemption,
    worth **$3,000 each** annually (WH-4 Line 4, per Indiana
    Departmental Notice #1 for tax years beginning after 2022;
    previously $1,500).
- `AdditionalWithholding` — extra per-pay state withholding requested on
  WH-4 Line 6.
- Pre-tax deductions that reduce state wages are subtracted before
  applying the per-period exemption allowance.
- Withholding is rounded to cents using `MidpointRounding.AwayFromZero`.

Indiana also levies county income tax that varies by county of residence
and principal work. That component is out of scope for this state
module and is not modeled by this calculator.
