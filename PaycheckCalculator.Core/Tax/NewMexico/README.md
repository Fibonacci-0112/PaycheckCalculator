# New Mexico (NM)

New Mexico state income tax withholding is computed by the dedicated
`NewMexicoWithholdingCalculator`, which implements the annualized
percentage-method formula described in the NM Taxation and Revenue
Department FYI-104 and Form RPD-41272 (New Mexico Employee's Withholding
Exemption Certificate).

The calculator supports three filing statuses (Single, Married, and Head of
Household), the $4,000 per-exemption deduction from RPD-41272, and five
graduated brackets (1.7%/3.2%/4.7%/4.9%/5.9%) with filing-status–specific
thresholds per NMSA §7-2-7 as amended by NM SB 145 (2023, effective 2024).
