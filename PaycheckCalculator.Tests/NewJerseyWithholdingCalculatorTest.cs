using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.NewJersey;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Regression tests for New Jersey (NJ) state income tax withholding.
/// New Jersey uses the dedicated <see cref="NewJerseyWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the NJ-WT annualized
/// percentage-method formula (2026):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − allowances × $1,000)
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 New Jersey parameters (NJ-WT):
///   Per-allowance deduction: $1,000
///   No standard deduction.
///   Table A — Single (Status A) and Married Filing Separately (Status C):
///     1.40% on $0 – $20,000
///     1.75% on $20,000 – $35,000
///     3.50% on $35,000 – $40,000
///     5.53% on $40,000 – $75,000
///     6.37% on $75,000 – $500,000
///     8.97% on $500,000 – $1,000,000
///     10.75% over $1,000,000
///   Table B — Married/Civil Union (Status B), Head of Household (Status D),
///   and Surviving Partner (Status E):
///     1.40% on $0 – $20,000
///     1.75% on $20,000 – $50,000
///     2.45% on $50,000 – $70,000
///     3.50% on $70,000 – $80,000
///     5.53% on $80,000 – $150,000
///     6.37% on $150,000 – $500,000
///     8.97% on $500,000 – $1,000,000
///     10.75% over $1,000,000
/// </summary>
public class NewJerseyWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNewJersey()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.NJ, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsStatusA_HasFiveOptions()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal(NewJerseyWithholdingCalculator.StatusA, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(5, field.Options!.Count);
        Assert.Contains(NewJerseyWithholdingCalculator.StatusA, field.Options);
        Assert.Contains(NewJerseyWithholdingCalculator.StatusB, field.Options);
        Assert.Contains(NewJerseyWithholdingCalculator.StatusC, field.Options);
        Assert.Contains(NewJerseyWithholdingCalculator.StatusD, field.Options);
        Assert.Contains(NewJerseyWithholdingCalculator.StatusE, field.Options);
    }

    // ── Status A (Single) — Table A brackets ────────────────────────

    [Fact]
    public void StatusA_Biweekly_FirstBracket()
    {
        // annual = $500 × 26 = $13,000
        // tax = $13,000 × 1.40% = $182.00
        // per period = $182.00 / 26 = $7.00
        var result = Calculate(GrossWages: 500m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(500m, result.TaxableWages);
        Assert.Equal(7.00m, result.Withholding);
    }

    [Fact]
    public void StatusA_Biweekly_SecondBracket()
    {
        // annual = $1,000 × 26 = $26,000
        // tax = $20,000 × 1.40% + $6,000 × 1.75%
        //     = $280.00 + $105.00 = $385.00
        // per period = $385.00 / 26 = $14.807... → $14.81
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(14.81m, result.Withholding);
    }

    [Fact]
    public void StatusA_Monthly_BelowBracket2_ReturnsFirstBracketOnly()
    {
        // annual = $1,500 × 12 = $18,000
        // tax = $18,000 × 1.40% = $252.00
        // per period = $252.00 / 12 = $21.00
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(1_500m, result.TaxableWages);
        Assert.Equal(21.00m, result.Withholding);
    }

    [Fact]
    public void StatusA_Biweekly_FifthBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50%
        //     + $35,000 × 5.53% + $3,000 × 6.37%
        //     = $280.00 + $262.50 + $175.00 + $1,935.50 + $191.10 = $2,844.10
        // per period = $2,844.10 / 26 = $109.388... → $109.39
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(109.39m, result.Withholding);
    }

    [Fact]
    public void StatusA_Biweekly_SixthBracket()
    {
        // annual = $25,000 × 26 = $650,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50%
        //     + $35,000 × 5.53% + $425,000 × 6.37% + $150,000 × 8.97%
        //     = $280.00 + $262.50 + $175.00 + $1,935.50 + $27,072.50 + $13,455.00
        //     = $43,180.50
        // per period = $43,180.50 / 26 = $1,660.788... → $1,660.79
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(25_000m, result.TaxableWages);
        Assert.Equal(1_660.79m, result.Withholding);
    }

    [Fact]
    public void StatusA_Biweekly_TopBracketAboveOneMillion()
    {
        // annual = $50,000 × 26 = $1,300,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50%
        //     + $35,000 × 5.53% + $425,000 × 6.37% + $500,000 × 8.97%
        //     + $300,000 × 10.75%
        //     = $280.00 + $262.50 + $175.00 + $1,935.50 + $27,072.50
        //       + $44,850.00 + $32,250.00 = $106,825.50
        // per period = $106,825.50 / 26 = $4,108.673... → $4,108.67
        var result = Calculate(GrossWages: 50_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(50_000m, result.TaxableWages);
        Assert.Equal(4_108.67m, result.Withholding);
    }

    // ── Status B (Married/Civil Union) — Table B brackets ────────────

    [Fact]
    public void StatusB_Biweekly_ThirdBracket()
    {
        // annual = $2,500 × 26 = $65,000
        // tax = $20,000 × 1.40% + $30,000 × 1.75% + $15,000 × 2.45%
        //     = $280.00 + $525.00 + $367.50 = $1,172.50
        // per period = $1,172.50 / 26 = $45.096... → $45.10
        var result = Calculate(GrossWages: 2_500m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(45.10m, result.Withholding);
    }

    [Fact]
    public void StatusB_Monthly_SecondBracket()
    {
        // annual = $4,000 × 12 = $48,000
        // tax = $20,000 × 1.40% + $28,000 × 1.75%
        //     = $280.00 + $490.00 = $770.00
        // per period = $770.00 / 12 = $64.166... → $64.17
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Monthly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(64.17m, result.Withholding);
    }

    [Fact]
    public void StatusB_Biweekly_FifthBracket()
    {
        // annual = $3,000 × 26 = $78,000
        // tax = $20,000 × 1.40% + $30,000 × 1.75% + $20,000 × 2.45%
        //     + $8,000 × 3.50%
        //     = $280.00 + $525.00 + $490.00 + $280.00 = $1,575.00
        // per period = $1,575.00 / 26 = $60.576... → $60.58
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(60.58m, result.Withholding);
    }

    [Fact]
    public void StatusB_Biweekly_TopBracketAboveOneMillion()
    {
        // annual = $25,000 × 26 = $650,000
        // tax = $20,000 × 1.40% + $30,000 × 1.75% + $20,000 × 2.45%
        //     + $10,000 × 3.50% + $70,000 × 5.53% + $350,000 × 6.37%
        //     + $150,000 × 8.97%
        //     = $280.00 + $525.00 + $490.00 + $350.00 + $3,871.00
        //       + $22,295.00 + $13,455.00 = $41,266.00
        // per period = $41,266.00 / 26 = $1,587.153... → $1,587.15
        var result = Calculate(GrossWages: 25_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(25_000m, result.TaxableWages);
        Assert.Equal(1_587.15m, result.Withholding);
    }

    // ── Status C (Married Filing Separately) — Table A brackets ──────

    [Fact]
    public void StatusC_UsesTableABrackets_SameAsSingleForSameWages()
    {
        // Status C uses Table A (single) brackets — same as Status A.
        // annual = $2,000 × 26 = $52,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50% + $12,000 × 5.53%
        //     = $280.00 + $262.50 + $175.00 + $663.60 = $1,381.10
        // per period = $1,381.10 / 26 = $53.119... → $53.12
        var resultC = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusC);
        var resultA = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(53.12m, resultC.Withholding);
        Assert.Equal(resultA.Withholding, resultC.Withholding);
    }

    // ── Status D (Head of Household) — Table B brackets ──────────────

    [Fact]
    public void StatusD_UsesTableBBrackets_SameAsMarriedForSameWages()
    {
        // Status D uses Table B (married) brackets — same as Status B.
        // annual = $3,000 × 26 = $78,000
        // per period same as StatusB biweekly $3,000 → $60.58
        var resultD = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusD);
        var resultB = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(60.58m, resultD.Withholding);
        Assert.Equal(resultB.Withholding, resultD.Withholding);
    }

    // ── Status E (Surviving Partner) — Table B brackets ──────────────

    [Fact]
    public void StatusE_UsesTableBBrackets_SameAsMarriedForSameWages()
    {
        // Status E uses Table B (married) brackets — same as Status B.
        // annual = $2,000 × 12 = $24,000
        // tax = $20,000 × 1.40% + $4,000 × 1.75%
        //     = $280.00 + $70.00 = $350.00
        // per period = $350.00 / 12 = $29.166... → $29.17
        var resultE = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, NewJerseyWithholdingCalculator.StatusE);
        var resultB = Calculate(GrossWages: 2_000m, PayFrequency.Monthly, NewJerseyWithholdingCalculator.StatusB);

        Assert.Equal(29.17m, resultE.Withholding);
        Assert.Equal(resultB.Withholding, resultE.Withholding);
    }

    // ── NJ-W4 Allowances ────────────────────────────────────────────

    [Fact]
    public void StatusA_Biweekly_TwoAllowances_ReduceTaxableIncome()
    {
        // annual = $3,000 × 26 = $78,000; taxable = $78,000 − 2 × $1,000 = $76,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50%
        //     + $35,000 × 5.53% + $1,000 × 6.37%
        //     = $280.00 + $262.50 + $175.00 + $1,935.50 + $63.70 = $2,716.70
        // per period = $2,716.70 / 26 = $104.488... → $104.49
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly,
            NewJerseyWithholdingCalculator.StatusA, allowances: 2);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(104.49m, result.Withholding);
    }

    [Fact]
    public void Allowances_EliminateAllTax_ReturnsZero()
    {
        // 30 allowances = $30,000 credit against $26,000 annual wages → $0 taxable
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly,
            NewJerseyWithholdingCalculator.StatusA, allowances: 30);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ───────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // Base per-period (Status A monthly $1,500) = $21.00; extra = $20 → $41.00
        var result = Calculate(GrossWages: 1_500m, PayFrequency.Monthly,
            NewJerseyWithholdingCalculator.StatusA, additionalWithholding: 20m);

        Assert.Equal(41.00m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50% + $25,000 × 5.53%
        //     = $280.00 + $262.50 + $175.00 + $1,382.50 = $2,100.00
        // per period = $2,100.00 / 26 = $80.769... → $80.77
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly,
            NewJerseyWithholdingCalculator.StatusA, preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(80.77m, result.Withholding);
    }

    // ── Semimonthly pay frequency ───────────────────────────────────

    [Fact]
    public void StatusA_Semimonthly_FourthBracket()
    {
        // annual = $2,000 × 24 = $48,000
        // tax = $20,000 × 1.40% + $15,000 × 1.75% + $5,000 × 3.50% + $8,000 × 5.53%
        //     = $280.00 + $262.50 + $175.00 + $442.40 = $1,159.90
        // per period = $1,159.90 / 24 = $48.329... → $48.33
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Semimonthly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(48.33m, result.Withholding);
    }

    // ── Zero gross wages ────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly, NewJerseyWithholdingCalculator.StatusA);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewJerseyWithholdingCalculator.StatusA,
            ["Allowances"] = -1
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Allowances", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewJerseyWithholdingCalculator.StatusA,
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewJerseyWithholdingCalculator.StatusB,
            ["Allowances"] = 3,
            ["AdditionalWithholding"] = 25m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AllFiveStatusCodes_AreValid()
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        foreach (var status in new[]
        {
            NewJerseyWithholdingCalculator.StatusA,
            NewJerseyWithholdingCalculator.StatusB,
            NewJerseyWithholdingCalculator.StatusC,
            NewJerseyWithholdingCalculator.StatusD,
            NewJerseyWithholdingCalculator.StatusE
        })
        {
            var values = new StateInputValues { ["FilingStatus"] = status };
            var errors = calc.Validate(values);
            Assert.Empty(errors);
        }
    }

    // ── Helper ──────────────────────────────────────────────────────

    private static StateWithholdingResult Calculate(
        decimal GrossWages,
        PayFrequency PayPeriod,
        string filingStatus,
        int allowances = 0,
        decimal additionalWithholding = 0m,
        decimal preTaxDeductions = 0m)
    {
        var calc = new NewJerseyWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.NJ,
            GrossWages: GrossWages,
            PayPeriod: PayPeriod,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxDeductions);
        var values = new StateInputValues
        {
            ["FilingStatus"] = filingStatus,
            ["Allowances"] = allowances,
            ["AdditionalWithholding"] = additionalWithholding
        };
        return calc.Calculate(context, values);
    }
}
