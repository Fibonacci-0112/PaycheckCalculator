# Idaho (ID)

Idaho state income tax withholding is computed by
[`IdahoWithholdingCalculator`](./IdahoWithholdingCalculator.cs), a dedicated
`IStateWithholdingCalculator` that implements the annualized percentage-method
"computer formula" from the Idaho State Tax Commission publication
[EPB00006, *A Guide to Idaho Income Tax Withholding*](https://tax.idaho.gov/).

Key 2026 values encoded in the calculator:

- Flat income tax rate: **5.3%** (Idaho HB 521, 2024, effective tax year 2024+)
- Standard deduction (Idaho conforms to the federal standard deduction per
  Idaho Code § 63-3022):
  - Single / HoH / MFS: **$16,100**
  - Married Filing Jointly: **$32,200**
- Allowance amount: **$3,300** per pre-2020 ID W-4 allowance
- Low-income exemption: withholding is $0 when annual taxable income after
  the standard deduction and allowances is ≤ $0.

The calculator is registered alongside the other dedicated state modules in
`MauiProgram.cs` and `PaycheckCalculator.Blazor/Program.cs`; the Idaho entry has
been removed from
[`StateTaxConfigs2026`](../State/StateTaxConfigs2026.cs) so the generic
percentage-method adapter no longer shadows it.
