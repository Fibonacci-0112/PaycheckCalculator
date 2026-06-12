using System.Text.Json;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Shared.Json;
using PaycheckCalc.Shared.Snapshots;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests that a saved snapshot survives a JSON round-trip through <see cref="PaycheckJson.Options"/>
/// without losing data the calculators depend on — in particular the <see cref="StateInputValues"/>
/// bag, whose values must come back as real CLR primitives (not <see cref="JsonElement"/>) so
/// <see cref="StateInputValues.GetValueOrDefault{T}"/> keeps working. This is the highest-risk part of
/// the sync feature: a serialization mismatch would silently corrupt stored paychecks.
/// </summary>
public sealed class PaycheckSnapshotJsonTest
{
    private static T RoundTrip<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, PaycheckJson.Options), PaycheckJson.Options)!;

    [Fact]
    public void StateInputValues_PrimitiveTypes_RoundTripViaGetValueOrDefault()
    {
        var input = new PaycheckInput
        {
            State = UsState.CA,
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",          // string
                ["Exempt"] = true,                    // bool
                ["Allowances"] = 3,                   // int
                ["AdditionalWithholding"] = 12.50m,   // decimal with a fractional part
                ["Nothing"] = null                    // null
            }
        };

        var rt = RoundTrip(input);

        Assert.NotNull(rt.StateInputValues);
        Assert.Equal("Single", rt.StateInputValues!.GetValueOrDefault("FilingStatus", ""));
        Assert.True(rt.StateInputValues.GetValueOrDefault("Exempt", false));
        Assert.Equal(3, rt.StateInputValues.GetValueOrDefault("Allowances", 0));
        Assert.Equal(12.50m, rt.StateInputValues.GetValueOrDefault("AdditionalWithholding", 0m));
        Assert.True(rt.StateInputValues.ContainsKey("Nothing"));
        Assert.Null(rt.StateInputValues["Nothing"]);
    }

    [Fact]
    public void StateInputValues_IntegralDecimal_StillReadsAsDecimal()
    {
        // 2m serializes as "2" and reads back as int; GetValueOrDefault<decimal> must still coerce it.
        var input = new PaycheckInput
        {
            StateInputValues = new StateInputValues { ["Amount"] = 2m }
        };

        var rt = RoundTrip(input);

        Assert.Equal(2m, rt.StateInputValues!.GetValueOrDefault("Amount", 0m));
    }

    [Fact]
    public void NullStateInputValues_RoundTripsAsNull()
    {
        var rt = RoundTrip(new PaycheckInput { State = UsState.TX });
        Assert.Null(rt.StateInputValues);
    }

    [Fact]
    public void Deductions_FederalW4_AndEnums_RoundTrip()
    {
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            State = UsState.CO,
            PayType = PayType.Salary,
            SalaryAmount = 90000m,
            SalaryBasis = SalaryBasis.PerYear,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.MarriedFilingJointly,
                Step2Checked = true,
                Step4cExtraWithholding = 25m
            },
            Deductions = new[]
            {
                new Deduction
                {
                    Name = "401k",
                    Type = DeductionType.PreTax,
                    Amount = 5m,
                    AmountType = DeductionAmountType.Percentage,
                    ReducesFicaWages = false
                },
                new Deduction { Name = "Roth", Type = DeductionType.PostTax, Amount = 100m }
            }
        };

        var rt = RoundTrip(input);

        Assert.Equal(PayFrequency.Biweekly, rt.Frequency);
        Assert.Equal(UsState.CO, rt.State);
        Assert.Equal(PayType.Salary, rt.PayType);
        Assert.Equal(FederalFilingStatus.MarriedFilingJointly, rt.FederalW4.FilingStatus);
        Assert.True(rt.FederalW4.Step2Checked);
        Assert.Equal(25m, rt.FederalW4.Step4cExtraWithholding);
        Assert.Equal(2, rt.Deductions.Count);
        Assert.Equal("401k", rt.Deductions[0].Name);
        Assert.Equal(DeductionType.PreTax, rt.Deductions[0].Type);
        Assert.Equal(DeductionAmountType.Percentage, rt.Deductions[0].AmountType);
        Assert.False(rt.Deductions[0].ReducesFicaWages);
        Assert.Equal("Roth", rt.Deductions[1].Name);
    }

    [Fact]
    public void Enums_SerializeAsNames_NotOrdinals()
    {
        var json = JsonSerializer.Serialize(
            new PaycheckInput { Frequency = PayFrequency.Monthly, State = UsState.CA },
            PaycheckJson.Options);

        Assert.Contains("Monthly", json);
        Assert.Contains("CA", json);
        Assert.DoesNotContain("\"frequency\":3", json);
    }

    [Fact]
    public void FullSnapshot_RoundTrips()
    {
        var dto = new SavedPaycheckDto
        {
            Name = "Job A",
            UpdatedAtUtc = new DateTimeOffset(2026, 6, 12, 9, 30, 0, TimeSpan.Zero),
            Input = new PaycheckInput
            {
                Frequency = PayFrequency.Biweekly,
                State = UsState.TX,
                HourlyRate = 25m,
                RegularHours = 80m,
                YtdSocialSecurityWages = 1000m
            },
            Result = new SavedPaycheckResultDto
            {
                GrossPay = 2000m,
                NetPay = 1700m,
                TotalTaxes = 300m,
                IsGrossUp = false
            }
        };

        var rt = RoundTrip(dto);

        Assert.Equal("Job A", rt.Name);
        Assert.Equal(dto.UpdatedAtUtc, rt.UpdatedAtUtc);
        Assert.Equal(1, rt.SchemaVersion);
        Assert.Equal(PayFrequency.Biweekly, rt.Input.Frequency);
        Assert.Equal(1000m, rt.Input.YtdSocialSecurityWages);
        Assert.Equal(1700m, rt.Result.NetPay);
    }

    [Fact]
    public void RoundTrippedInput_ProducesIdenticalCalculation()
    {
        // A comprehensive Texas input (hourly + overtime + W-4 extras + pre-tax 401k). After a JSON
        // round-trip, the calculator must produce byte-identical results — proving no input field is
        // lost or coerced in a way that changes the math.
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            HourlyRate = 30m,
            RegularHours = 80m,
            OvertimeHours = 5m,
            OvertimeMultiplier = 1.5m,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.HeadOfHousehold,
                Step4cExtraWithholding = 15m
            },
            Deductions = new[]
            {
                new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 6m, AmountType = DeductionAmountType.Percentage, ReducesFicaWages = false }
            }
        };

        var calculator = CreateTexasPayCalculator();
        var original = calculator.Calculate(input);
        var afterRoundTrip = calculator.Calculate(RoundTrip(input));

        Assert.Equal(original.GrossPay, afterRoundTrip.GrossPay);
        Assert.Equal(original.FederalWithholding, afterRoundTrip.FederalWithholding);
        Assert.Equal(original.SocialSecurityWithholding, afterRoundTrip.SocialSecurityWithholding);
        Assert.Equal(original.MedicareWithholding, afterRoundTrip.MedicareWithholding);
        Assert.Equal(original.PreTaxDeductions, afterRoundTrip.PreTaxDeductions);
        Assert.Equal(original.NetPay, afterRoundTrip.NetPay);
    }

    private static PayCalculator CreateTexasPayCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fed = new Irs15TPercentageCalculator(File.ReadAllText("us_irs_15t_2026_percentage_automated.json"));
        return new PayCalculator(registry, fica, fed);
    }
}
