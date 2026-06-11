using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.NewYork;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for New York (NY) state income tax withholding.
/// New York uses the dedicated <see cref="NewYorkWithholdingCalculator"/>.
///
/// Expected dollar amounts are hand-computed from the NYS-50-T-NYS annualized
/// percentage-method formula (2026):
///   annual taxable = max(0, (per-period wages − pre-tax deductions) × periods
///                         − standard deduction − allowances × $1,000)
///   annual tax     = graduated brackets applied to annual taxable
///   per-period     = round(annual tax ÷ periods, 2) + extra withholding
///
/// 2026 New York parameters (NYS Publication NYS-50-T-NYS):
///   Standard deduction:
///     Single / MFS:          $8,000
///     Married / QW:          $16,050
///     Head of Household:     $11,000
///   Per-allowance deduction (IT-2104): $1,000 per allowance
///   Single and Head of Household brackets:
///     4.00%  on $0 – $8,500
///     4.50%  on $8,500 – $11,700
///     5.25%  on $11,700 – $13,900
///     5.90%  on $13,900 – $21,400
///     6.09%  on $21,400 – $80,650
///     6.41%  on $80,650 – $215,400
///     6.85%  on $215,400 – $1,077,550
///     9.65%  on $1,077,550 – $5,000,000
///     10.30% on $5,000,000 – $25,000,000
///     10.90% over $25,000,000
///   Married (MFJ / QW) brackets:
///     4.00%  on $0 – $17,150
///     4.50%  on $17,150 – $23,600
///     5.25%  on $23,600 – $27,900
///     5.90%  on $27,900 – $43,000
///     6.09%  on $43,000 – $161,550
///     6.41%  on $161,550 – $323,200
///     6.85%  on $323,200 – $2,155,350
///     9.65%  on $2,155,350 – $5,000,000
///     10.30% on $5,000,000 – $25,000,000
///     10.90% over $25,000,000
/// </summary>
public class NewYorkWithholdingCalculatorTest
{
    // ── State identity ──────────────────────────────────────────────

    [Fact]
    public void State_ReturnsNewYork()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        Assert.Equal(UsState.NY, calc.State);
    }

    // ── Schema ──────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsFilingStatus_Allowances_AdditionalWithholding()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Single(schema, f => f.Key == "FilingStatus");
        Assert.Single(schema, f => f.Key == "Allowances");
        Assert.Single(schema, f => f.Key == "AdditionalWithholding");
    }

    [Fact]
    public void Schema_FilingStatus_DefaultsSingle_HasThreeOptions()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var field = Assert.Single(calc.GetInputSchema(), f => f.Key == "FilingStatus");

        Assert.Equal(NewYorkWithholdingCalculator.StatusSingle, field.DefaultValue);
        Assert.NotNull(field.Options);
        Assert.Equal(3, field.Options!.Count);
        Assert.Contains(NewYorkWithholdingCalculator.StatusSingle, field.Options);
        Assert.Contains(NewYorkWithholdingCalculator.StatusMarried, field.Options);
        Assert.Contains(NewYorkWithholdingCalculator.StatusHeadOfHousehold, field.Options);
    }

    // ── Single — first two brackets ─────────────────────────────────

    [Fact]
    public void Single_Biweekly_FirstBracketOnly()
    {
        // annual = $250 × 26 = $6,500; taxable = $6,500 − $8,000 = −$1,500 → $0
        // tax = $0
        var result = Calculate(GrossWages: 250m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(250m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Single_Monthly_SecondBracket()
    {
        // annual = $1,000 × 12 = $12,000; taxable = $12,000 − $8,000 = $4,000
        // 4% × $4,000 = $160.00
        // per period = $160.00 / 12 = $13.333... → $13.33
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(1_000m, result.TaxableWages);
        Assert.Equal(13.33m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_FifthBracket()
    {
        // annual = $3,000 × 26 = $78,000; taxable = $78,000 − $8,000 = $70,000
        // 4.00% × $8,500         =   $340.00
        // 4.50% × $3,200         =   $144.00  ($11,700 − $8,500)
        // 5.25% × $2,200         =   $115.50  ($13,900 − $11,700)
        // 5.90% × $7,500         =   $442.50  ($21,400 − $13,900)
        // 6.09% × $48,600        = $2,959.74  ($70,000 − $21,400)
        // total                  = $4,001.74
        // per period = $4,001.74 / 26 = $153.913... → $153.91
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(3_000m, result.TaxableWages);
        Assert.Equal(153.91m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_SixthBracket()
    {
        // annual = $4,000 × 26 = $104,000; taxable = $104,000 − $8,000 = $96,000
        // 4.00% × $8,500         =   $340.00
        // 4.50% × $3,200         =   $144.00
        // 5.25% × $2,200         =   $115.50
        // 5.90% × $7,500         =   $442.50
        // 6.09% × $59,250        = $3,608.325  ($80,650 − $21,400)
        // 6.41% × $15,350        =   $983.935  ($96,000 − $80,650)
        // total                  = $5,634.26
        // per period = $5,634.26 / 26 = $216.702... → $216.70
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(216.70m, result.Withholding);
    }

    [Fact]
    public void Single_Biweekly_TopBracketAboveTwentyFiveMillion()
    {
        // annual = $1,200,000 × 26 = $31,200,000;
        // taxable = $31,200,000 − $8,000 = $31,192,000
        // 4.00%  × $8,500           =       $340.00
        // 4.50%  × $3,200           =       $144.00
        // 5.25%  × $2,200           =       $115.50
        // 5.90%  × $7,500           =       $442.50
        // 6.09%  × $59,250          =     $3,608.325
        // 6.41%  × $134,750         =     $8,637.475
        // 6.85%  × $862,150         =    $59,057.275  ($1,077,550 − $215,400)
        // 9.65%  × $3,922,450       =   $378,516.425
        // 10.30% × $20,000,000      = $2,060,000.00
        // 10.90% × $6,192,000       =   $674,928.00
        // total = $3,185,789.50
        // per period = $3,185,789.50 / 26 = $122,530.365... → $122,530.37
        var result = Calculate(GrossWages: 1_200_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(1_200_000m, result.TaxableWages);
        Assert.Equal(122_530.37m, result.Withholding);
    }

    // ── Married — all bracket tiers ─────────────────────────────────

    [Fact]
    public void Married_Biweekly_FourthBracket()
    {
        // annual = $2,000 × 26 = $52,000; taxable = $52,000 − $16,050 = $35,950
        // 4.00% × $17,150     =   $686.00
        // 4.50% × $6,450      =   $290.25  ($23,600 − $17,150)
        // 5.25% × $4,300      =   $225.75  ($27,900 − $23,600)
        // 5.90% × $8,050      =   $474.95  ($35,950 − $27,900)
        // total               = $1,676.95
        // per period = $1,676.95 / 26 = $64.498... → $64.50
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusMarried);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(64.50m, result.Withholding);
    }

    [Fact]
    public void Married_Biweekly_FifthBracket()
    {
        // annual = $4,000 × 26 = $104,000; taxable = $104,000 − $16,050 = $87,950
        // 4.00% × $17,150     =   $686.00
        // 4.50% × $6,450      =   $290.25
        // 5.25% × $4,300      =   $225.75
        // 5.90% × $15,100     =   $890.90  ($43,000 − $27,900)
        // 6.09% × $44,950     = $2,737.455 ($87,950 − $43,000)
        // total               = $4,830.355
        // per period = $4,830.355 / 26 = $185.782... → $185.78
        var result = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusMarried);

        Assert.Equal(4_000m, result.TaxableWages);
        Assert.Equal(185.78m, result.Withholding);
    }

    [Fact]
    public void Married_Monthly_SixthBracket()
    {
        // annual = $15,000 × 12 = $180,000; taxable = $180,000 − $16,050 = $163,950
        // 4.00% × $17,150     =   $686.00
        // 4.50% × $6,450      =   $290.25
        // 5.25% × $4,300      =   $225.75
        // 5.90% × $15,100     =   $890.90
        // 6.09% × $118,550    = $7,219.695  ($161,550 − $43,000)
        // 6.41% × $2,400      =   $153.84   ($163,950 − $161,550)
        // total               = $9,466.435
        // per period = $9,466.435 / 12 = $788.869... → $788.87
        var result = Calculate(GrossWages: 15_000m, PayFrequency.Monthly,
            NewYorkWithholdingCalculator.StatusMarried);

        Assert.Equal(15_000m, result.TaxableWages);
        Assert.Equal(788.87m, result.Withholding);
    }

    // ── Head of Household ────────────────────────────────────────────

    [Fact]
    public void HeadOfHousehold_Monthly_FifthBracket()
    {
        // annual = $5,000 × 12 = $60,000; taxable = $60,000 − $11,000 = $49,000
        // 4.00% × $8,500      =   $340.00
        // 4.50% × $3,200      =   $144.00
        // 5.25% × $2,200      =   $115.50
        // 5.90% × $7,500      =   $442.50
        // 6.09% × $27,600     = $1,680.84  ($49,000 − $21,400)
        // total               = $2,722.84
        // per period = $2,722.84 / 12 = $226.903... → $226.90
        var result = Calculate(GrossWages: 5_000m, PayFrequency.Monthly,
            NewYorkWithholdingCalculator.StatusHeadOfHousehold);

        Assert.Equal(5_000m, result.TaxableWages);
        Assert.Equal(226.90m, result.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_UsesSingleBrackets_DifferentFromMarried()
    {
        // HoH uses Single brackets (not Married brackets), with $11,000 std deduction.
        // Same wages as Married → different taxable income and different brackets.
        var hohResult    = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusHeadOfHousehold);
        var marriedResult = Calculate(GrossWages: 4_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusMarried);

        // HoH should differ from Married because std ded and brackets differ.
        Assert.NotEqual(hohResult.Withholding, marriedResult.Withholding);
    }

    [Fact]
    public void HeadOfHousehold_StandardDeductionDifferentFromSingle()
    {
        // HoH std ded = $11,000; Single std ded = $8,000.
        // HoH taxable is smaller → lower withholding for the same wages.
        var hohResult    = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusHeadOfHousehold);
        var singleResult = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        // HoH's larger deduction produces lower withholding at the same wage level.
        Assert.True(hohResult.Withholding < singleResult.Withholding,
            "HoH withholding should be lower than Single at the same wage due to larger standard deduction.");
    }

    // ── IT-2104 Allowances ───────────────────────────────────────────

    [Fact]
    public void Single_Biweekly_TwoAllowances_ReduceTaxableIncome()
    {
        // annual = $2,000 × 26 = $52,000; taxable = $52,000 − $8,000 − (2 × $1,000) = $42,000
        // 4.00% × $8,500      =   $340.00
        // 4.50% × $3,200      =   $144.00
        // 5.25% × $2,200      =   $115.50
        // 5.90% × $7,500      =   $442.50
        // 6.09% × $20,600     = $1,254.54  ($42,000 − $21,400)
        // total               = $2,296.54
        // per period = $2,296.54 / 26 = $88.328... → $88.33
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle, allowances: 2);

        Assert.Equal(2_000m, result.TaxableWages);
        Assert.Equal(88.33m, result.Withholding);
    }

    [Fact]
    public void Allowances_EliminateAllTax_ReturnsZero()
    {
        // 100 allowances × $1,000 = $100,000 well above any reasonable annual wage → $0 taxable
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle, allowances: 100);

        Assert.Equal(0m, result.Withholding);
    }

    // ── Additional withholding ───────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAddedToBaseAmount()
    {
        // base (Single monthly $1,000) = $13.33; extra = $25 → $38.33
        var result = Calculate(GrossWages: 1_000m, PayFrequency.Monthly,
            NewYorkWithholdingCalculator.StatusSingle, additionalWithholding: 25m);

        Assert.Equal(38.33m, result.Withholding);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWagesAndWithholding()
    {
        // gross $3,000, pre-tax $500 → taxable wages = $2,500
        // annual = $2,500 × 26 = $65,000; taxable = $65,000 − $8,000 = $57,000
        // 4.00% × $8,500      =   $340.00
        // 4.50% × $3,200      =   $144.00
        // 5.25% × $2,200      =   $115.50
        // 5.90% × $7,500      =   $442.50
        // 6.09% × $35,600     = $2,168.04  ($57,000 − $21,400)
        // total               = $3,210.04
        // per period = $3,210.04 / 26 = $123.463... → $123.46
        var result = Calculate(GrossWages: 3_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle, preTaxDeductions: 500m);

        Assert.Equal(2_500m, result.TaxableWages);
        Assert.Equal(123.46m, result.Withholding);
    }

    // ── Zero gross wages ────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var result = Calculate(GrossWages: 0m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── Semimonthly pay frequency ────────────────────────────────────

    [Fact]
    public void Single_Semimonthly_ThirdBracket()
    {
        // annual = $800 × 24 = $19,200; taxable = $19,200 − $8,000 = $11,200
        // 4.00% × $8,500      = $340.00
        // 4.50% × $2,700      = $121.50  ($11,200 − $8,500)
        // total               = $461.50
        // per period = $461.50 / 24 = $19.229... → $19.23
        var result = Calculate(GrossWages: 800m, PayFrequency.Semimonthly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.Equal(800m, result.TaxableWages);
        Assert.Equal(19.23m, result.Withholding);
    }

    // ── Explanation ─────────────────────────────────────────────────

    [Fact]
    public void Explanation_Single_ShowsDeductionBracketsAndDeannualization()
    {
        // $2,000 biweekly Single, 0 allowances:
        // annualize = 52,000; std ded = 8,000; annual taxable = 44,000
        // tax = 340 + 144 + 115.50 + 442.50 + (22,600 × 6.09%) = 2,418.34
        // per period = 2,418.34 / 26 = 93.013... → 93.01
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle);

        Assert.NotNull(result.WithholdingSteps);
        var annualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annualize wages"));
        Assert.Equal(52_000m, annualizeStep.Value);
        var dedStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Less standard deduction"));
        Assert.Equal(8_000m, dedStep.Value);
        var taxableStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Annual taxable income");
        Assert.Equal(44_000m, taxableStep.Value);
        var bracketStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annual tax from New York's graduated brackets"));
        Assert.Equal(2_418.34m, bracketStep.Value);
        // Top dollar falls in the 6.09% bracket over $21,400
        Assert.Contains("6.09", bracketStep.Detail);
        Assert.Contains("$21,400", bracketStep.Detail);
        var deannualizeStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "De-annualize to this pay period");
        Assert.Equal(93.01m, deannualizeStep.Value);
        Assert.Contains("NYS-50-T-NYS", result.WithholdingReference);
    }

    [Fact]
    public void Explanation_Married_UsesMarriedScheduleLabel()
    {
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusMarried);

        var bracketStep = Assert.Single(result.WithholdingSteps!, s => s.Label.StartsWith("Annual tax from New York's graduated brackets"));
        Assert.Contains("Married", bracketStep.Label);
    }

    [Fact]
    public void Explanation_Allowances_EmitAllowanceDeductionStep()
    {
        // 3 allowances × $1,000 = $3,000 annual deduction
        var result = Calculate(GrossWages: 2_000m, PayFrequency.Biweekly,
            NewYorkWithholdingCalculator.StatusSingle, allowances: 3);

        var allowanceStep = Assert.Single(result.WithholdingSteps!, s => s.Label == "Less IT-2104 allowance deduction");
        Assert.Equal(3_000m, allowanceStep.Value);
    }

    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidFilingStatus_ReturnsError()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues { ["FilingStatus"] = "InvalidStatus" };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Filing Status", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAllowances_ReturnsError()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewYorkWithholdingCalculator.StatusSingle,
            ["Allowances"] = -1
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Allowances", errors[0]);
    }

    [Fact]
    public void Validate_NegativeAdditionalWithholding_ReturnsError()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewYorkWithholdingCalculator.StatusSingle,
            ["AdditionalWithholding"] = -5m
        };

        var errors = calc.Validate(values);

        Assert.Single(errors);
        Assert.Contains("Additional Withholding", errors[0]);
    }

    [Fact]
    public void Validate_ValidInputs_ReturnsNoErrors()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var values = new StateInputValues
        {
            ["FilingStatus"] = NewYorkWithholdingCalculator.StatusMarried,
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 50m
        };

        var errors = calc.Validate(values);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AllThreeStatuses_AreValid()
    {
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        foreach (var status in new[]
        {
            NewYorkWithholdingCalculator.StatusSingle,
            NewYorkWithholdingCalculator.StatusMarried,
            NewYorkWithholdingCalculator.StatusHeadOfHousehold
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
        var calc = new NewYorkWithholdingCalculator(TestSchemas.Provider);
        var context = new CommonWithholdingContext(
            UsState.NY,
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
