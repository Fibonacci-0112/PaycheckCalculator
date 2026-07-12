using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Supplemental;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="BonusCalculator"/> — the supplemental-wage (bonus) orchestrator that
/// composes the federal flat-rate method, FICA, and state supplemental withholding. Expected
/// values are computed explicitly from the rates (federal 22%/37%, SS 6.2% to the $184,500
/// cap, Medicare 1.45%, Additional Medicare 0.9% over $200k, and the state's published
/// supplemental rate), not recomputed with the production calculators.
/// </summary>
public sealed class BonusCalculatorTest
{
    [Fact]
    public void Bonus_NoStateTax_FederalPlusFica()
    {
        // $5,000 bonus, Texas (no state tax), no YTD:
        //   Federal  = 5,000 × 22%   = 1,100.00
        //   SS       = 5,000 × 6.2%  =   310.00
        //   Medicare = 5,000 × 1.45% =    72.50
        //   State    = 0
        //   Net      = 5,000 − 1,482.50 = 3,517.50
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.TX });

        Assert.Equal(1_100.00m, result.FederalWithholding);
        Assert.Equal(310.00m, result.SocialSecurityWithholding);
        Assert.Equal(72.50m, result.MedicareWithholding);
        Assert.Equal(0m, result.AdditionalMedicareWithholding);
        Assert.Equal(0m, result.StateWithholding);
        Assert.Equal(1_482.50m, result.TotalTaxes);
        Assert.Equal(3_517.50m, result.NetBonus);
    }

    [Fact]
    public void Bonus_CaliforniaFlatRate_AddsStateWithholding()
    {
        // $5,000 bonus, California (10.23% supplemental):
        //   Federal 1,100.00 + SS 310.00 + Medicare 72.50 + State 511.50 = 1,994.00
        //   Net = 5,000 − 1,994.00 = 3,006.00
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.CA });

        Assert.Equal(511.50m, result.StateWithholding);
        Assert.Equal(StateSupplementalMethod.FlatRate, result.StateMethod);
        Assert.False(result.StateUsesRegularMethod);
        Assert.Equal(1_994.00m, result.TotalTaxes);
        Assert.Equal(3_006.00m, result.NetBonus);
    }

    [Fact]
    public void Bonus_Oklahoma_FlatRate()
    {
        // $10,000 bonus, Oklahoma (4.5%):
        //   Federal 2,200.00 + SS 620.00 + Medicare 145.00 + State 450.00 = 3,415.00
        //   Net = 10,000 − 3,415.00 = 6,585.00
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 10_000m, State = UsState.OK });

        Assert.Equal(2_200.00m, result.FederalWithholding);
        Assert.Equal(620.00m, result.SocialSecurityWithholding);
        Assert.Equal(145.00m, result.MedicareWithholding);
        Assert.Equal(450.00m, result.StateWithholding);
        Assert.Equal(6_585.00m, result.NetBonus);
    }

    [Fact]
    public void Bonus_Vermont_StateIs30PercentOfFederal()
    {
        // $5,000 bonus, Vermont (30% of federal withholding):
        //   Federal 1,100.00 → State = 1,100 × 30% = 330.00
        //   Net = 5,000 − (1,100 + 310 + 72.50 + 330) = 3,187.50
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.VT });

        Assert.Equal(StateSupplementalMethod.FederalPercentage, result.StateMethod);
        Assert.Equal(330.00m, result.StateWithholding);
        Assert.Equal(3_187.50m, result.NetBonus);
    }

    [Fact]
    public void Bonus_RegularMethodState_FlagsAndExcludesStateTax()
    {
        // Pennsylvania uses the regular method: state withholding is not estimated (0)
        // and the result is flagged so the net is understood to be before state income tax.
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.PA });

        Assert.Equal(StateSupplementalMethod.RegularMethod, result.StateMethod);
        Assert.True(result.StateUsesRegularMethod);
        Assert.Equal(0m, result.StateWithholding);
        Assert.Equal(3_517.50m, result.NetBonus); // same as the no-state-tax case
    }

    [Fact]
    public void Bonus_RespectsSocialSecurityWageCap()
    {
        // $5,000 bonus, Texas, but $182,000 of SS wages already earned this year.
        //   Remaining SS base = 184,500 − 182,000 = 2,500
        //   SS = 2,500 × 6.2% = 155.00 (not 310.00)
        //   Medicare = 5,000 × 1.45% = 72.50 (no cap)
        var calc = Create();
        var result = calc.Calculate(new BonusInput
        {
            BonusAmount = 5_000m,
            State = UsState.TX,
            YtdSocialSecurityWages = 182_000m,
            YtdMedicareWages = 182_000m
        });

        Assert.Equal(155.00m, result.SocialSecurityWithholding);
        Assert.Equal(72.50m, result.MedicareWithholding);
    }

    [Fact]
    public void Bonus_AppliesAdditionalMedicareOverThreshold()
    {
        // $50,000 bonus, Texas, $180,000 Medicare wages already earned:
        //   Crosses the $200,000 Additional Medicare threshold by 30,000.
        //   Additional Medicare = 30,000 × 0.9% = 270.00
        //   Regular Medicare    = 50,000 × 1.45% = 725.00
        var calc = Create();
        var result = calc.Calculate(new BonusInput
        {
            BonusAmount = 50_000m,
            State = UsState.TX,
            YtdSocialSecurityWages = 180_000m,
            YtdMedicareWages = 180_000m
        });

        Assert.Equal(725.00m, result.MedicareWithholding);
        Assert.Equal(270.00m, result.AdditionalMedicareWithholding);
    }

    [Fact]
    public void Bonus_OverMillionDollars_HighEarner()
    {
        // $2,000,000 bonus, Texas, SS and Medicare wage bases already met ($184,500 each):
        //   Federal = 1,000,000 × 22% + 1,000,000 × 37% = 590,000.00
        //   SS      = 0 (wage base already met)
        //   Medicare= 2,000,000 × 1.45% = 29,000.00
        //   Addl Medicare: prior 184,500 → current 2,184,500; over-threshold portion
        //                  = 2,184,500 − 200,000 = 1,984,500 → × 0.9% = 17,860.50
        //   Net = 2,000,000 − (590,000 + 0 + 29,000 + 17,860.50) = 1,363,139.50
        var calc = Create();
        var result = calc.Calculate(new BonusInput
        {
            BonusAmount = 2_000_000m,
            State = UsState.TX,
            YtdSocialSecurityWages = 184_500m,
            YtdMedicareWages = 184_500m
        });

        Assert.Equal(590_000.00m, result.FederalWithholding);
        Assert.Equal(0m, result.SocialSecurityWithholding);
        Assert.Equal(29_000.00m, result.MedicareWithholding);
        Assert.Equal(17_860.50m, result.AdditionalMedicareWithholding);
        Assert.Equal(1_363_139.50m, result.NetBonus);
    }

    [Fact]
    public void Bonus_ComponentsSumToNet()
    {
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 7_345.67m, State = UsState.CA });

        Assert.Equal(result.BonusAmount - result.TotalTaxes, result.NetBonus);
    }

    [Fact]
    public void Bonus_ZeroAmount_AllZero()
    {
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 0m, State = UsState.CA });

        Assert.Equal(0m, result.FederalWithholding);
        Assert.Equal(0m, result.StateWithholding);
        Assert.Equal(0m, result.NetBonus);
    }

    [Fact]
    public void Bonus_NegativeAmount_Throws()
    {
        var calc = Create();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => calc.Calculate(new BonusInput { BonusAmount = -1m, State = UsState.TX }));
    }

    [Fact]
    public void Bonus_PopulatesExplanationLines()
    {
        var calc = Create();
        var result = calc.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.CA });

        // Bonus amount, federal, SS, Medicare, state, and net lines are all present.
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.GrossPay));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.FederalWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.StateWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.NetPay));
    }

    private static BonusCalculator Create()
    {
        var federal = new FederalSupplementalCalculator();
        var fica = new FicaCalculator();
        var state = new StateSupplementalCalculator(File.ReadAllText("state_supplemental_2026.json"));
        return new BonusCalculator(federal, fica, state);
    }
}
