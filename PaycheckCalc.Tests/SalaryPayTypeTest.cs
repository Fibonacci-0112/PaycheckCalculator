using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="PayType"/> / <see cref="SalaryBasis"/> support: salaried gross
/// pay (annual or per-period) computed alongside the existing hourly path. Expected
/// values are taken directly from the salary ÷ pay-periods arithmetic, not recomputed
/// with production helpers.
/// </summary>
public sealed class SalaryPayTypeTest
{
    // ── PayPeriods.PerYear ────────────────────────────────────────

    [Theory]
    [InlineData(PayFrequency.Weekly, 52)]
    [InlineData(PayFrequency.Biweekly, 26)]
    [InlineData(PayFrequency.Semimonthly, 24)]
    [InlineData(PayFrequency.Monthly, 12)]
    [InlineData(PayFrequency.Quarterly, 4)]
    [InlineData(PayFrequency.Semiannual, 2)]
    [InlineData(PayFrequency.Annual, 1)]
    [InlineData(PayFrequency.Daily, 260)]
    [InlineData(PayFrequency.Weekly53, 53)]
    [InlineData(PayFrequency.Biweekly27, 27)]
    public void PayPeriods_PerYear_ReturnsExpected(PayFrequency frequency, int expected)
    {
        Assert.Equal(expected, PayPeriods.PerYear(frequency));
    }

    // ── Defaults / backward compatibility ─────────────────────────

    [Fact]
    public void PaycheckInput_DefaultPayType_IsHourly()
    {
        var input = new PaycheckInput();

        Assert.Equal(PayType.Hourly, input.PayType);
        Assert.Equal(SalaryBasis.PerYear, input.SalaryBasis);
        Assert.Equal(0m, input.SalaryAmount);
    }

    [Fact]
    public void Hourly_IgnoresSalaryAmount()
    {
        // PayType defaults to Hourly; a stray SalaryAmount must not affect the result.
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            SalaryAmount = 999_999m, // should be ignored in hourly mode
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay); // 40 × $50
    }

    // ── Salary: per period ────────────────────────────────────────

    [Fact]
    public void Salary_PerPeriod_GrossEqualsSalaryAmount()
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = 2000m,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
    }

    [Fact]
    public void Salary_PerPeriod_EquivalentToHourlySameGross()
    {
        // A $2,000/period salary should produce identical taxes/net to 40 hrs × $50.
        var calculator = CreateCalculator();

        var hourly = calculator.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX
        });

        var salary = calculator.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = 2000m,
            State = UsState.TX
        });

        Assert.Equal(hourly.GrossPay, salary.GrossPay);
        Assert.Equal(hourly.FederalWithholding, salary.FederalWithholding);
        Assert.Equal(hourly.SocialSecurityWithholding, salary.SocialSecurityWithholding);
        Assert.Equal(hourly.MedicareWithholding, salary.MedicareWithholding);
        Assert.Equal(hourly.StateWithholding, salary.StateWithholding);
        Assert.Equal(hourly.NetPay, salary.NetPay);
    }

    // ── Salary: per year ──────────────────────────────────────────

    [Theory]
    [InlineData(PayFrequency.Weekly, 52000, 1000)]    // 52000 / 52
    [InlineData(PayFrequency.Biweekly, 52000, 2000)]  // 52000 / 26
    [InlineData(PayFrequency.Monthly, 60000, 5000)]   // 60000 / 12
    [InlineData(PayFrequency.Annual, 75000, 75000)]   // 75000 / 1
    public void Salary_PerYear_DividesEvenlyByPayPeriods(PayFrequency frequency, decimal annual, decimal expectedGross)
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = frequency,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerYear,
            SalaryAmount = annual,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(expectedGross, result.GrossPay);
    }

    [Fact]
    public void Salary_PerYear_NonEvenDivision_RoundsGrossToCent()
    {
        // $50,000 / 26 = 1923.0769... → 1923.08 (rounded away from zero).
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerYear,
            SalaryAmount = 50000m,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(1923.08m, result.GrossPay);
    }

    // ── Salary + deductions ───────────────────────────────────────

    [Fact]
    public void Salary_PreTaxDeduction_ReducesTaxableWages()
    {
        // $4,000/period gross, $500 pre-tax 401(k) → federal taxable income $3,500.
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = 4000m,
            State = UsState.TX,
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401k",
                    Type = DeductionType.PreTax,
                    Amount = 500m,
                    AmountType = DeductionAmountType.Dollar,
                    ReducesFederalTaxableWages = true
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(4000m, result.GrossPay);
        Assert.Equal(500m, result.PreTaxDeductions);
        Assert.Equal(3500m, result.FederalTaxableIncome);
    }

    // ── Salary explanation ("Show Your Work") ─────────────────────

    [Fact]
    public void Salary_PerYear_GrossExplanation_ShowsAnnualDivision()
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerYear,
            SalaryAmount = 52000m,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        var gross = result.Explanation.Get(ExplanationLineKey.GrossPay);
        Assert.NotNull(gross);
        Assert.Equal(2000m, gross!.FinalAmount);
        Assert.Contains(gross.Steps, s => s.Label == "Annual salary");
        Assert.Contains(gross.Steps, s => s.Formula is not null && s.Formula.Contains("÷"));
    }

    [Fact]
    public void Salary_PerPeriod_GrossExplanation_ShowsSingleStep()
    {
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            PayType = PayType.Salary,
            SalaryBasis = SalaryBasis.PerPeriod,
            SalaryAmount = 2000m,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        var gross = result.Explanation.Get(ExplanationLineKey.GrossPay);
        Assert.NotNull(gross);
        Assert.Equal(2000m, gross!.FinalAmount);
        Assert.Contains(gross.Steps, s => s.Label == "Gross pay per period");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static PayCalculator CreateCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText("us_irs_15t_2026_percentage_automated.json");
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
