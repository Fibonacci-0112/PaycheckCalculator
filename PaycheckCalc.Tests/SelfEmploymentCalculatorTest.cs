using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="SelfEmploymentCalculator"/> — the 1099 / self-employment estimator
/// that computes federal self-employment (SE) tax on 92.35% of net earnings, estimates
/// state income tax via the ordinary state engine, and builds a quarterly estimated-payment
/// schedule. Expected values are computed explicitly from the SE-tax rates — 12.4% Social
/// Security to the $184,500 wage base, 2.9% Medicare, 0.9% Additional Medicare over $200,000,
/// applied to net earnings × 0.9235 — not recomputed with the production calculator.
/// </summary>
public sealed class SelfEmploymentCalculatorTest
{
    [Fact]
    public void Se_NoStateTax_BasicEarnings()
    {
        // $100,000 net profit, Texas (no income tax), no YTD wages:
        //   SE base  = 100,000 × 0.9235 = 92,350.00
        //   SS       = 92,350 × 12.4%   = 11,451.40
        //   Medicare = 92,350 × 2.9%    =  2,678.15
        //   SE tax   = 14,129.55
        //   State    = 0
        //   Take-home = 100,000 − 14,129.55 = 85,870.45
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 100_000m, State = UsState.TX });

        Assert.Equal(92_350.00m, result.NetEarningsSubjectToSeTax);
        Assert.Equal(11_451.40m, result.SocialSecurityTax);
        Assert.Equal(2_678.15m, result.MedicareTax);
        Assert.Equal(0m, result.AdditionalMedicareTax);
        Assert.Equal(14_129.55m, result.SelfEmploymentTax);
        Assert.Equal(0m, result.StateIncomeTax);
        Assert.Equal(85_870.45m, result.TakeHomeBeforeStateTax);
        Assert.Equal(85_870.45m, result.TakeHome);
        Assert.Equal(14_129.55m, result.TotalTax);
    }

    [Fact]
    public void Se_SocialSecurityWageBaseCap_Applied()
    {
        // $250,000 net profit, Texas:
        //   SE base = 250,000 × 0.9235 = 230,875.00 (exceeds the $184,500 SS cap)
        //   SS       = 184,500 × 12.4% = 22,878.00 (capped)
        //   Medicare = 230,875 × 2.9%  =  6,695.38
        //   Addl Med = (230,875 − 200,000) × 0.9% = 30,875 × 0.9% = 277.88
        //   SE tax   = 29,851.26
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 250_000m, State = UsState.TX });

        Assert.Equal(230_875.00m, result.NetEarningsSubjectToSeTax);
        Assert.Equal(22_878.00m, result.SocialSecurityTax);
        Assert.Equal(6_695.38m, result.MedicareTax);
        Assert.Equal(277.88m, result.AdditionalMedicareTax);
        Assert.Equal(29_851.26m, result.SelfEmploymentTax);
    }

    [Fact]
    public void Se_YtdSocialSecurityWages_ReduceRemainingBase()
    {
        // $50,000 net profit, Texas, with $160,000 of W-2 SS wages already earned:
        //   SE base   = 50,000 × 0.9235 = 46,175.00
        //   Remaining SS base = 184,500 − 160,000 = 24,500
        //   SS        = 24,500 × 12.4% = 3,038.00 (limited by remaining base)
        //   Medicare  = 46,175 × 2.9%  = 1,339.08
        //   SE tax    = 4,377.08
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput
        {
            AnnualNetEarnings = 50_000m,
            State = UsState.TX,
            YtdSocialSecurityWages = 160_000m
        });

        Assert.Equal(3_038.00m, result.SocialSecurityTax);
        Assert.Equal(1_339.08m, result.MedicareTax);
        Assert.Equal(4_377.08m, result.SelfEmploymentTax);
    }

    [Fact]
    public void Se_AdditionalMedicare_CrossesThresholdWithPriorWages()
    {
        // $50,000 net profit, Texas, with $180,000 of prior Medicare wages:
        //   SE base   = 46,175.00
        //   SS        = 46,175 × 12.4% = 5,725.70 (under the cap)
        //   Medicare  = 46,175 × 2.9%  = 1,339.08
        //   Addl Med  = (180,000 + 46,175 − 200,000) × 0.9% = 26,175 × 0.9% = 235.58
        //   SE tax    = 7,300.36
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput
        {
            AnnualNetEarnings = 50_000m,
            State = UsState.TX,
            YtdMedicareWages = 180_000m
        });

        Assert.Equal(5_725.70m, result.SocialSecurityTax);
        Assert.Equal(1_339.08m, result.MedicareTax);
        Assert.Equal(235.58m, result.AdditionalMedicareTax);
        Assert.Equal(7_300.36m, result.SelfEmploymentTax);
    }

    [Fact]
    public void Se_StateIncomeTax_EstimatedOnFullEarnings_PennsylvaniaFlat()
    {
        // $100,000 net profit, Pennsylvania (flat 3.07% on the full earnings):
        //   SE tax    = 14,129.55 (as above)
        //   State     = 100,000 × 3.07% = 3,070.00
        //   Take-home = 100,000 − 14,129.55 − 3,070.00 = 82,800.45
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 100_000m, State = UsState.PA });

        Assert.Equal(14_129.55m, result.SelfEmploymentTax);
        Assert.Equal(3_070.00m, result.StateIncomeTax);
        Assert.Equal(85_870.45m, result.TakeHomeBeforeStateTax);
        Assert.Equal(82_800.45m, result.TakeHome);
        Assert.Equal(17_199.55m, result.TotalTax);
    }

    [Fact]
    public void Se_QuarterlyEstimates_SplitEvenlyAndSumToAnnual()
    {
        // SE tax 14,129.55, PA state 3,070.00. Quarterly = annual ÷ 4 with the remainder
        // absorbed by Q4.
        //   Federal: 3,532.39 × 3 + 3,532.38 = 14,129.55
        //   State:   767.50 × 4 = 3,070.00
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 100_000m, State = UsState.PA });

        Assert.Equal(4, result.QuarterlyEstimates.Count);
        Assert.Equal(3_532.39m, result.FederalQuarterlyPayment);
        Assert.Equal(767.50m, result.StateQuarterlyPayment);

        Assert.Equal(result.SelfEmploymentTax, result.QuarterlyEstimates.Sum(q => q.FederalAmount));
        Assert.Equal(result.StateIncomeTax, result.QuarterlyEstimates.Sum(q => q.StateAmount));
        Assert.Equal(result.TotalTax, result.QuarterlyEstimates.Sum(q => q.TotalAmount));

        Assert.Equal(3_532.38m, result.QuarterlyEstimates[3].FederalAmount); // Q4 absorbs the remainder
    }

    [Fact]
    public void Se_QuarterlyEstimates_HaveStandard1040EsDueDates()
    {
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 100_000m, State = UsState.TX });

        Assert.Collection(result.QuarterlyEstimates,
            q => Assert.Equal(new DateOnly(2026, 4, 15), q.DueDate),
            q => Assert.Equal(new DateOnly(2026, 6, 15), q.DueDate),
            q => Assert.Equal(new DateOnly(2026, 9, 15), q.DueDate),
            q => Assert.Equal(new DateOnly(2027, 1, 15), q.DueDate));
    }

    [Fact]
    public void Se_ZeroEarnings_ProducesAllZeroTaxes()
    {
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 0m, State = UsState.PA });

        Assert.Equal(0m, result.SelfEmploymentTax);
        Assert.Equal(0m, result.StateIncomeTax);
        Assert.Equal(0m, result.TakeHome);
        Assert.All(result.QuarterlyEstimates, q => Assert.Equal(0m, q.TotalAmount));
    }

    [Fact]
    public void Se_NegativeEarnings_Throws()
    {
        var calc = Create();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = -1m, State = UsState.TX }));
    }

    [Fact]
    public void Se_Explanation_HasLineForEachVisibleRow()
    {
        var calc = Create();
        var result = calc.Calculate(new SelfEmploymentInput { AnnualNetEarnings = 250_000m, State = UsState.PA });

        var e = result.Explanation;
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.GrossPay));            // net SE income
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.FicaTaxableWages));    // 92.35% base
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.SocialSecurity));
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.Medicare));
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.AdditionalMedicare));  // present at $250k
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.FederalWithholding));  // SE tax total
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.StateWithholding));    // state income tax
        Assert.NotNull(e.Get(Core.Explanation.ExplanationLineKey.NetPay));              // take-home
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static SelfEmploymentCalculator Create()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        registry.Register(new PennsylvaniaWithholdingCalculator());
        return new SelfEmploymentCalculator(registry, new FicaCalculator());
    }
}
