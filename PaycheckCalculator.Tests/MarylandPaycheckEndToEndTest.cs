using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.Federal;
using PaycheckCalculator.Core.Tax.State;
using Xunit;

namespace PaycheckCalculator.Tests;

/// <summary>
/// End-to-end regression for the Maryland reference paycheck, run through the
/// real DI-wired pipeline rather than the state calculator alone.
///
/// The scenario is the one the Comptroller's percentage method is easiest to
/// check by hand, and the one ADP and PaycheckCity agree on: 80 hours at $20.00
/// bi-weekly, single, Carroll County, zero exemptions, no deductions.
///
///   Gross                                            $1,600.00
///   Federal income tax                                   $67.12
///   Social Security   $1,600.00 × 6.20%                  $99.20
///   Medicare          $1,600.00 × 1.45%                  $23.20
///   Maryland state    $1,469.24 × 4.75%                  $69.79
///   Maryland county   $1,469.24 × 3.03%                  $44.52
///   Total taxes                                         $303.83
///   Take home                                         $1,296.17
///
/// where the Maryland taxable income is $1,600.00 less the bi-weekly standard
/// deduction allowance of $130.76 (guide page 10).
///
/// The two filing statuses differ on purpose, as they may in real life: the
/// federal W-4 claims head of household, while Form MW507 claims single. Federal
/// withholding is then Pub 15-T Worksheet 1A — annual wages $41,600, less the
/// $8,600 Step-2-unchecked adjustment, giving an adjusted annual wage of $33,000,
/// which falls in the head-of-household 10% band: ($33,000 − $15,550) × 10% =
/// $1,745.00 ÷ 26 = $67.12.
/// </summary>
public sealed class MarylandPaycheckEndToEndTest
{
    private static PaycheckResult ReferencePaycheck()
        => PayCalculatorTestHarness.Calculate(new PaycheckInput
        {
            State = UsState.MD,
            PayType = PayType.Hourly,
            HourlyRate = 20m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.HeadOfHousehold
            },
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["County"] = "Carroll County",
                ["Exemptions"] = 0,
                ["AdditionalWithholding"] = 0m
            }
        });

    [Fact]
    public void ReferencePaycheck_MarylandLinesMatchThePercentageMethod()
    {
        var result = ReferencePaycheck();

        var state = result.StateTaxLines.Single(l => l.Kind == StateTaxLineKind.StateIncome);
        var county = result.StateTaxLines.Single(l => l.Kind == StateTaxLineKind.CountyIncome);

        Assert.Equal(69.79m, state.Amount);
        Assert.Equal(44.52m, county.Amount);
        Assert.Equal("County Income Tax (Carroll County)", county.Label);
    }

    [Fact]
    public void ReferencePaycheck_StateLinesAreOrderedStateThenCounty()
    {
        var result = ReferencePaycheck();

        Assert.Equal(
            [StateTaxLineKind.StateIncome, StateTaxLineKind.CountyIncome],
            result.StateTaxLines.Select(l => l.Kind));
    }

    [Fact]
    public void ReferencePaycheck_EveryLineTotalAndNetPayMatch()
    {
        var result = ReferencePaycheck();

        Assert.Equal(1_600.00m, result.GrossPay);
        Assert.Equal(67.12m, result.FederalWithholding);
        Assert.Equal(99.20m, result.SocialSecurityWithholding);
        Assert.Equal(23.20m, result.MedicareWithholding);

        // The scalar keeps the combined state figure the Comptroller's tables
        // report, while the lines stay itemized: $69.79 + $44.52.
        Assert.Equal(114.31m, result.StateWithholding);
        Assert.Equal(0m, result.StateDisabilityInsurance);

        Assert.Equal(303.83m, result.TotalTaxes);
        Assert.Equal(1_296.17m, result.NetPay);
    }

    [Fact]
    public void ReferencePaycheck_NetPayIsGrossLessEveryWithheldLine()
    {
        var result = ReferencePaycheck();

        var withheld = result.FederalWithholding
                     + result.SocialSecurityWithholding
                     + result.MedicareWithholding
                     + result.AdditionalMedicareWithholding
                     + result.StateWithholding
                     + result.StateDisabilityInsurance;

        Assert.Equal(withheld, result.TotalTaxes);
        Assert.Equal(result.GrossPay - withheld, result.NetPay);
    }
}
