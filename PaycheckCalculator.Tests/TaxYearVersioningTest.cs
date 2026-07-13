using System.Text.Json;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Pay;
using PaycheckCalculator.Core.Tax.Fica;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using PaycheckCalculator.Core.Tax.Supplemental;
using PaycheckCalculator.Shared.Json;
using PaycheckCalculator.Shared.Snapshots;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// Tests for tax-year versioning: <see cref="TaxYearSupport"/> and the <c>TaxYear</c> field now
/// carried on every input/result model. Only 2026 tax data is loaded today, so
/// <see cref="TaxYearSupport.Default"/> is both the implicit default for new inputs and the only
/// currently-supported year; calculators must reject any other year rather than silently applying
/// 2026 tables to it.
/// </summary>
public sealed class TaxYearVersioningTest
{
    [Fact]
    public void TaxYearSupport_Default_Is2026()
    {
        Assert.Equal(2026, TaxYearSupport.Default);
    }

    [Theory]
    [InlineData(2026, true)]
    [InlineData(2025, false)]
    [InlineData(2027, false)]
    public void TaxYearSupport_IsSupported_OnlyMatchesDefault(int year, bool expected)
    {
        Assert.Equal(expected, TaxYearSupport.IsSupported(year));
    }

    [Fact]
    public void PaycheckInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new PaycheckInput().TaxYear);
    }

    [Fact]
    public void SelfEmploymentInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new SelfEmploymentInput().TaxYear);
    }

    [Fact]
    public void BonusInput_DefaultTaxYear_IsSupportedDefault()
    {
        Assert.Equal(TaxYearSupport.Default, new BonusInput().TaxYear);
    }

    // ── PayCalculator ──────────────────────────────────────────────

    [Fact]
    public void PayCalculator_Result_CarriesInputTaxYear()
    {
        var calculator = CreatePayCalculator();
        var result = calculator.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX,
            TaxYear = 2026
        });

        Assert.Equal(2026, result.TaxYear);
    }

    [Fact]
    public void PayCalculator_UnsupportedTaxYear_ThrowsNotSupportedException()
    {
        var calculator = CreatePayCalculator();
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 50m,
            RegularHours = 40m,
            State = UsState.TX,
            TaxYear = 2027
        };

        Assert.Throws<NotSupportedException>(() => calculator.Calculate(input));
    }

    // ── SelfEmploymentCalculator ───────────────────────────────────

    [Fact]
    public void SelfEmploymentCalculator_Result_CarriesInputTaxYear()
    {
        var calculator = CreateSelfEmploymentCalculator();
        var result = calculator.Calculate(new SelfEmploymentInput
        {
            AnnualNetEarnings = 100_000m,
            State = UsState.TX,
            TaxYear = 2026
        });

        Assert.Equal(2026, result.TaxYear);
    }

    [Fact]
    public void SelfEmploymentCalculator_UnsupportedTaxYear_ThrowsNotSupportedException()
    {
        var calculator = CreateSelfEmploymentCalculator();
        var input = new SelfEmploymentInput
        {
            AnnualNetEarnings = 100_000m,
            State = UsState.TX,
            TaxYear = 2030
        };

        Assert.Throws<NotSupportedException>(() => calculator.Calculate(input));
    }

    // ── BonusCalculator ─────────────────────────────────────────────

    [Fact]
    public void BonusCalculator_Result_CarriesInputTaxYear()
    {
        var calculator = CreateBonusCalculator();
        var result = calculator.Calculate(new BonusInput { BonusAmount = 5_000m, State = UsState.TX, TaxYear = 2026 });

        Assert.Equal(2026, result.TaxYear);
    }

    [Fact]
    public void BonusCalculator_UnsupportedTaxYear_ThrowsNotSupportedException()
    {
        var calculator = CreateBonusCalculator();
        var input = new BonusInput { BonusAmount = 5_000m, State = UsState.TX, TaxYear = 1999 };

        Assert.Throws<NotSupportedException>(() => calculator.Calculate(input));
    }

    // ── Snapshot round-trip ────────────────────────────────────────

    [Fact]
    public void SavedPaycheckResultMapper_CopiesTaxYearFromResult()
    {
        var result = new PaycheckResult { GrossPay = 2000m, NetPay = 1700m, TaxYear = 2026 };

        var dto = SavedPaycheckResultMapper.FromResult(result);

        Assert.Equal(2026, dto.TaxYear);
    }

    [Fact]
    public void SavedPaycheckDto_TaxYear_RoundTripsThroughJson()
    {
        var dto = new SavedPaycheckDto
        {
            Name = "Job A",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Input = new PaycheckInput { State = UsState.TX, TaxYear = 2026 },
            Result = new SavedPaycheckResultDto { NetPay = 1700m, TaxYear = 2026 }
        };

        var json = JsonSerializer.Serialize(dto, PaycheckJson.Options);
        var rt = JsonSerializer.Deserialize<SavedPaycheckDto>(json, PaycheckJson.Options)!;

        Assert.Equal(2026, rt.Input.TaxYear);
        Assert.Equal(2026, rt.Result.TaxYear);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static PayCalculator CreatePayCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        var fica = new FicaCalculator();
        var fed = new Irs15TPercentageCalculator(File.ReadAllText("us_irs_15t_2026_percentage_automated.json"));
        return new PayCalculator(registry, fica, fed);
    }

    private static SelfEmploymentCalculator CreateSelfEmploymentCalculator()
    {
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        return new SelfEmploymentCalculator(registry, new FicaCalculator());
    }

    private static BonusCalculator CreateBonusCalculator()
    {
        var federal = new FederalSupplementalCalculator();
        var fica = new FicaCalculator();
        var state = new StateSupplementalCalculator(File.ReadAllText("state_supplemental_2026.json"));
        return new BonusCalculator(federal, fica, state);
    }
}
