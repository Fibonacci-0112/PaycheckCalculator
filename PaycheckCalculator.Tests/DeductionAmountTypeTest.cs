using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for <see cref="DeductionAmountType"/> support, verifying that
/// deductions can be specified as either a fixed dollar amount or a
/// percentage of gross pay.
/// </summary>
public sealed class DeductionAmountTypeTest
{
    // ── Deduction.EffectiveAmount ──────────────────────────────────

    [Fact]
    public void EffectiveAmount_Dollar_ReturnsAmountDirectly()
    {
        var deduction = new Deduction
        {
            Name = "401k",
            Type = DeductionType.PreTax,
            Amount = 200m,
            AmountType = DeductionAmountType.Dollar
        };

        Assert.Equal(200m, deduction.EffectiveAmount(grossPay: 5000m));
    }

    [Fact]
    public void EffectiveAmount_Percentage_ComputesFromGross()
    {
        // 5% of $4,000 gross = $200
        var deduction = new Deduction
        {
            Name = "401k",
            Type = DeductionType.PreTax,
            Amount = 5m,
            AmountType = DeductionAmountType.Percentage
        };

        Assert.Equal(200m, deduction.EffectiveAmount(grossPay: 4000m));
    }

    [Fact]
    public void EffectiveAmount_Percentage_ZeroGross_ReturnsZero()
    {
        var deduction = new Deduction
        {
            Name = "401k",
            Type = DeductionType.PreTax,
            Amount = 10m,
            AmountType = DeductionAmountType.Percentage
        };

        Assert.Equal(0m, deduction.EffectiveAmount(grossPay: 0m));
    }

    [Fact]
    public void EffectiveAmount_DefaultAmountType_IsDollar()
    {
        var deduction = new Deduction { Name = "HSA", Amount = 150m };

        // Default AmountType should be Dollar, so EffectiveAmount returns the raw amount
        Assert.Equal(DeductionAmountType.Dollar, deduction.AmountType);
        Assert.Equal(150m, deduction.EffectiveAmount(grossPay: 3000m));
    }

    // ── PayCalculator integration ─────────────────────────────────

    [Fact]
    public void Calculator_PercentagePreTaxDeduction_ReducesNetPay()
    {
        // Gross: 40 hrs × $50/hr = $2,000
        // 10% pre-tax = $200 deducted before taxes
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX, // no state income tax
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401k",
                    Type = DeductionType.PreTax,
                    Amount = 10m,
                    AmountType = DeductionAmountType.Percentage,
                    ReducesStateTaxableWages = true
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(200m, result.PreTaxDeductions);
    }

    [Fact]
    public void Calculator_PercentagePostTaxDeduction_ReducesNetPay()
    {
        // Gross: 40 hrs × $50/hr = $2,000
        // 5% post-tax = $100 deducted after taxes
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX, // no state income tax
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "Roth 401k",
                    Type = DeductionType.PostTax,
                    Amount = 5m,
                    AmountType = DeductionAmountType.Percentage
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(100m, result.PostTaxDeductions);
    }

    [Fact]
    public void Calculator_MixedDollarAndPercentageDeductions()
    {
        // Gross: 40 hrs × $50/hr = $2,000
        // Pre-tax: $150 flat + 5% ($100) = $250 total
        // Post-tax: 2.5% ($50) = $50 total
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX, // no state income tax
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401k Flat",
                    Type = DeductionType.PreTax,
                    Amount = 150m,
                    AmountType = DeductionAmountType.Dollar
                },
                new Deduction
                {
                    Name = "401k Pct",
                    Type = DeductionType.PreTax,
                    Amount = 5m,
                    AmountType = DeductionAmountType.Percentage
                },
                new Deduction
                {
                    Name = "Charity",
                    Type = DeductionType.PostTax,
                    Amount = 2.5m,
                    AmountType = DeductionAmountType.Percentage
                }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(250m, result.PreTaxDeductions);  // $150 + 5% of $2000
        Assert.Equal(50m, result.PostTaxDeductions);   // 2.5% of $2000
    }

    [Fact]
    public void Calculator_DollarDeductions_BackwardCompatible()
    {
        // Verify that existing dollar-only deductions work exactly as before
        var calculator = CreateCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX,
            Deductions = new[]
            {
                new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 200m },
                new Deduction { Name = "Roth", Type = DeductionType.PostTax, Amount = 100m }
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(200m, result.PreTaxDeductions);
        Assert.Equal(100m, result.PostTaxDeductions);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static PayCalculator CreateCalculator()
    {
        var registry = new StateCalculatorRegistry();
        // TX is a no-income-tax state; use a simple adapter
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText("us_irs_15t_2026_percentage_automated.json");
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
