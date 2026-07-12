# Maine (ME)

Maine state income tax withholding is computed by the dedicated
`MaineWithholdingCalculator` in this folder.

## Formula (Maine Revenue Services, 2026 Withholding Tables)

1. Per-period state taxable wages = gross wages − pre-tax deductions
   (floored at $0).
2. Annualize wages (× pay periods per year).
3. Subtract the filing-status standard deduction.
4. Subtract W-4ME allowance amounts ($5,300 per allowance).
5. Floor the result at $0 (low-income exemption).
6. Apply the graduated income-tax brackets.
7. De-annualize (÷ pay periods) and round to two decimal places
   (midpoint away from zero).
8. Add any additional per-period withholding from Form W-4ME.

## 2026 Parameters

| Parameter | Single | Married |
|---|---|---|
| Standard deduction | $15,300 | $30,600 |
| Per-allowance deduction | $5,300 | $5,300 |
| Bracket 1 | 5.80% on $0 – $27,400 | 5.80% on $0 – $54,850 |
| Bracket 2 | 6.75% on $27,401 – $64,850 | 6.75% on $54,851 – $129,750 |
| Bracket 3 | 7.15% over $64,850 | 7.15% over $129,750 |

## Registration

`MaineWithholdingCalculator` is registered as a dedicated singleton in:
- `PaycheckCalculator.App/MauiProgram.cs`
- `PaycheckCalculator.Blazor/Program.cs`
- `PaycheckCalculator.Tests/StateWithholdingArchitectureTest.cs` (`BuildFullRegistry`)

## Tests

See `PaycheckCalculator.Tests/MaineWithholdingCalculatorTest.cs` for regression
tests covering all three brackets (single and married), allowances, extra
withholding, pre-tax deductions, and the low-income exemption.
