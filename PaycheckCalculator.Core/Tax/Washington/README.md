# Washington (WA)

Washington does not levy a state individual income tax.  The
`WashingtonWithholdingCalculator` returns zero income-tax withholding and adds
the mandatory **WA Cares Fund (Long-Term Care Insurance)** premium at **0.58 %**
of all gross wages each pay period (no wage-base cap).

Employees who hold a Department of Social and Health Services (DSHS)-approved
exemption certificate may opt out of the WA Cares Fund.  The calculator exposes
a `WaCaresExempt` boolean toggle field in its input schema for this purpose.

## Sources

- RCW 50B.04.080 (WA Cares Fund employee premium)
- Washington State DSHS WA Cares Fund Employer Information (2026)
