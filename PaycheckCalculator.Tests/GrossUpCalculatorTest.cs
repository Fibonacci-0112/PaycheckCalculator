using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Pennsylvania;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="GrossUpCalculator"/> — the inverse paycheck solver that finds
/// the gross pay needed to deliver a target net (take-home) amount. Expected gross
/// values are taken directly from the FICA / flat-rate arithmetic for scenarios where
/// federal withholding is $0 (annualized wages below the percentage-method threshold),
/// not recomputed with the production calculator.
///
/// FICA-only regime (no state tax, federal withholding $0): for a monthly check the
/// annualized wage stays under the $16,100 point where Single withholding begins, so
/// net = gross − 6.2% SS − 1.45% Medicare = gross × 0.9235.
/// </summary>
public sealed class GrossUpCalculatorTest
{
    // ── FICA-only explicit gross-up ───────────────────────────────

    [Fact]
    public void GrossUp_FicaOnly_Monthly_Tx_SolvesExactGross()
    {
        // Monthly, Texas (no state tax). Gross $1,000 annualizes to $12,000 — below the
        // $16,100 Single withholding threshold — so federal withholding is $0.
        //   SS       = 1000 × 6.2%  = 62.00
        //   Medicare = 1000 × 1.45% = 14.50
        //   Net      = 1000 − 76.50 = 923.50
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Monthly, UsState.TX);

        var result = grossUp.Calculate(input, 923.50m);

        Assert.Equal(1000.00m, result.GrossUpPay);
        Assert.Equal(923.50m, result.TargetNetPay);
        Assert.Equal(0m, result.Paycheck.FederalWithholding);
        Assert.Equal(62.00m, result.Paycheck.SocialSecurityWithholding);
        Assert.Equal(14.50m, result.Paycheck.MedicareWithholding);
        Assert.Equal(923.50m, result.ActualNetPay);
        Assert.True(result.Converged);
    }

    [Fact]
    public void GrossUp_FicaOnly_Biweekly_Tx_SolvesExactGross()
    {
        // Biweekly, Texas. Gross $600 annualizes to $15,600 (< $16,100) → federal $0.
        //   SS = 37.20, Medicare = 8.70, Net = 600 − 45.90 = 554.10
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Biweekly, UsState.TX);

        var result = grossUp.Calculate(input, 554.10m);

        Assert.Equal(600.00m, result.GrossUpPay);
        Assert.Equal(0m, result.Paycheck.FederalWithholding);
        Assert.Equal(37.20m, result.Paycheck.SocialSecurityWithholding);
        Assert.Equal(8.70m, result.Paycheck.MedicareWithholding);
        Assert.Equal(554.10m, result.ActualNetPay);
    }

    [Fact]
    public void GrossUp_FlatStateTax_Monthly_Pa_SolvesExactGross()
    {
        // Monthly, Pennsylvania (flat 3.07%). Gross $1,000 → federal $0 (as above).
        //   SS 62.00 + Medicare 14.50 + PA 30.70 = 107.20
        //   Net = 1000 − 107.20 = 892.80
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Monthly, UsState.PA);

        var result = grossUp.Calculate(input, 892.80m);

        Assert.Equal(1000.00m, result.GrossUpPay);
        Assert.Equal(0m, result.Paycheck.FederalWithholding);
        Assert.Equal(30.70m, result.Paycheck.StateWithholding);
        Assert.Equal(892.80m, result.ActualNetPay);
    }

    [Fact]
    public void GrossUp_PercentagePreTax401k_NotReducingFica_SolvesExactGross()
    {
        // Monthly, Texas, with a 10% pre-tax 401(k) that lowers federal/state wages but
        // is still subject to FICA. At gross $1,000:
        //   401(k) = 100.00 (pre-tax), FICA still on full 1,000 → SS 62.00 + Medicare 14.50
        //   Federal taxable = 900 → annualized 10,800 − 8,600 = 2,200 (< 7,500) → federal $0
        //   Net = 1000 − 100 − 76.50 = 823.50
        var grossUp = CreateGrossUp();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Monthly,
            State = UsState.TX,
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401k",
                    Type = DeductionType.PreTax,
                    Amount = 10m,
                    AmountType = DeductionAmountType.Percentage,
                    ReducesFederalTaxableWages = true,
                    ReducesStateTaxableWages = true,
                    ReducesFicaWages = false
                }
            }
        };

        var result = grossUp.Calculate(input, 823.50m);

        Assert.Equal(1000.00m, result.GrossUpPay);
        Assert.Equal(100.00m, result.Paycheck.PreTaxDeductions);
        Assert.Equal(0m, result.Paycheck.FederalWithholding);
        Assert.Equal(62.00m, result.Paycheck.SocialSecurityWithholding);
        Assert.Equal(823.50m, result.ActualNetPay);
    }

    // ── Edge cases ────────────────────────────────────────────────

    [Fact]
    public void GrossUp_ZeroTarget_ReturnsZeroGross()
    {
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Biweekly, UsState.TX);

        var result = grossUp.Calculate(input, 0m);

        Assert.Equal(0m, result.GrossUpPay);
        Assert.Equal(0m, result.ActualNetPay);
        Assert.True(result.Converged);
    }

    [Fact]
    public void GrossUp_NegativeTarget_Throws()
    {
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Biweekly, UsState.TX);

        Assert.Throws<ArgumentOutOfRangeException>(() => grossUp.Calculate(input, -1m));
    }

    [Fact]
    public void GrossUp_RoundsFractionalTargetToCent()
    {
        // 554.104 rounds to 554.10, giving the same $600 gross as the exact target.
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Biweekly, UsState.TX);

        var result = grossUp.Calculate(input, 554.104m);

        Assert.Equal(554.10m, result.TargetNetPay);
        Assert.Equal(600.00m, result.GrossUpPay);
    }

    // ── Result shape / derived figures ────────────────────────────

    [Fact]
    public void GrossUp_ReportsCostAndBreakdownAtSolvedGross()
    {
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Monthly, UsState.TX);

        var result = grossUp.Calculate(input, 923.50m);

        // The breakdown is computed at the solved gross, and cost is gross − target.
        Assert.Equal(result.GrossUpPay, result.Paycheck.GrossPay);
        Assert.Equal(result.Paycheck.NetPay, result.ActualNetPay);
        Assert.Equal(result.GrossUpPay - result.TargetNetPay, result.GrossUpCost);
        Assert.Equal(76.50m, result.GrossUpCost); // 62.00 SS + 14.50 Medicare
    }

    // ── Convergence across the federal brackets ───────────────────

    [Theory]
    [InlineData(PayFrequency.Weekly, 1000)]
    [InlineData(PayFrequency.Biweekly, 2500)]
    [InlineData(PayFrequency.Semimonthly, 3000)]
    [InlineData(PayFrequency.Monthly, 5000)]
    [InlineData(PayFrequency.Monthly, 12000)]
    public void GrossUp_ResultingNet_MeetsTargetWithinACent(PayFrequency frequency, decimal target)
    {
        // These targets push gross into the federal withholding brackets, so the solver
        // must invert the graduated IRS percentage method. The gross-up guarantee is
        // that the resulting net is at least the target, overshooting by at most a cent
        // (the granularity of a whole-cent gross).
        var grossUp = CreateGrossUp();
        var input = BaseInput(frequency, UsState.TX);

        var result = grossUp.Calculate(input, target);

        Assert.True(result.Converged);
        Assert.True(result.GrossUpPay >= target);
        Assert.True(result.ActualNetPay >= target,
            $"net {result.ActualNetPay} should be ≥ target {target}");
        Assert.True(result.ActualNetPay - target <= 0.02m,
            $"net {result.ActualNetPay} should overshoot target {target} by at most a cent or two");
        // Minimality: a cent less of gross must miss the target.
        if (result.GrossUpPay - 0.01m >= target)
            Assert.True(PayNetAt(input, result.GrossUpPay - 0.01m) < target,
                "one cent less of gross should net below the target");
    }

    [Fact]
    public void GrossUp_HigherTarget_RequiresHigherGross()
    {
        var grossUp = CreateGrossUp();
        var input = BaseInput(PayFrequency.Monthly, UsState.TX);

        var low = grossUp.Calculate(input, 2000m);
        var high = grossUp.Calculate(input, 4000m);

        Assert.True(high.GrossUpPay > low.GrossUpPay);
    }

    [Fact]
    public void GrossUp_SolvedGross_FedBackThroughPayCalculator_ReproducesNet()
    {
        // Feeding the solved gross back into the calculator as a per-period salary must
        // reproduce the same net the gross-up reported (the result is self-consistent).
        var grossUp = CreateGrossUp();
        var pay = CreatePayCalculator();
        var input = BaseInput(PayFrequency.Monthly, UsState.TX);

        var result = grossUp.Calculate(input, 5000m);
        var rerun = pay.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Monthly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = result.GrossUpPay,
            State = UsState.TX
        });

        Assert.Equal(result.ActualNetPay, rerun.NetPay);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static PaycheckInput BaseInput(PayFrequency frequency, UsState state) => new()
    {
        Frequency = frequency,
        State = state
    };

    private static decimal PayNetAt(PaycheckInput input, decimal gross)
    {
        var pay = CreatePayCalculator();
        return pay.Calculate(new PaycheckInput
        {
            Frequency = input.Frequency,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = gross,
            State = input.State,
            Deductions = input.Deductions
        }).NetPay;
    }

    private static GrossUpCalculator CreateGrossUp() => new(CreatePayCalculator());

    private static PayCalculator CreatePayCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        registry.Register(new PennsylvaniaWithholdingCalculator());
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText("us_irs_15t_2026_percentage_automated.json");
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
