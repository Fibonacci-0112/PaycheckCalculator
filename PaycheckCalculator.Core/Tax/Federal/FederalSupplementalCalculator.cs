using PaycheckCalculator.Core.Explanation;

namespace PaycheckCalculator.Core.Tax.Federal;

/// <summary>
/// Federal income-tax withholding on supplemental wages (bonuses, commissions, awards,
/// severance, etc.) using the optional <b>flat-rate method</b> from IRS Publication 15
/// (Circular E), Section 7.
///
/// Supplemental wages up to the cumulative annual threshold of $1,000,000 are withheld at
/// a flat 22%; any portion above $1,000,000 is withheld at the mandatory 37% rate. The
/// $1,000,000 threshold is cumulative across all supplemental wages an employer pays the
/// employee during the calendar year, so year-to-date supplemental wages are needed to
/// split this payment between the 22% and 37% portions correctly.
/// </summary>
public sealed class FederalSupplementalCalculator
{
    /// <summary>Optional flat rate on supplemental wages up to the $1M cumulative threshold.</summary>
    public const decimal FlatRate = 0.22m;

    /// <summary>Mandatory rate on cumulative supplemental wages above the $1M threshold.</summary>
    public const decimal MandatoryHighRate = 0.37m;

    /// <summary>Cumulative annual supplemental-wage threshold above which the 37% rate applies.</summary>
    public decimal MillionDollarThreshold { get; init; } = 1_000_000m;

    public decimal Calculate(decimal supplementalWages, decimal ytdSupplementalWages = 0m)
        => CalculateWithExplanation(supplementalWages, ytdSupplementalWages).Withholding;

    /// <summary>
    /// Computes federal supplemental withholding and a step-by-step
    /// <see cref="LineExplanation"/> mirroring the Pub 15 flat-rate method.
    /// </summary>
    /// <param name="supplementalWages">The supplemental payment (e.g. a bonus) for this run.</param>
    /// <param name="ytdSupplementalWages">
    /// Supplemental wages already paid to the employee earlier in the calendar year, used to
    /// place this payment relative to the $1,000,000 cumulative threshold.
    /// </param>
    public (decimal Withholding, LineExplanation Explanation) CalculateWithExplanation(
        decimal supplementalWages, decimal ytdSupplementalWages = 0m)
    {
        var wages = Math.Max(0m, supplementalWages);
        var ytd = Math.Max(0m, ytdSupplementalWages);

        // How much of the first $1M of cumulative supplemental wages this payment can still
        // use (taxed at 22%); the remainder of this payment is taxed at 37%.
        var remainingUnderThreshold = Math.Max(0m, MillionDollarThreshold - ytd);
        var lowPortion = Math.Min(wages, remainingUnderThreshold);
        var highPortion = wages - lowPortion;

        var lowTax = lowPortion * FlatRate;
        var highTax = highPortion * MandatoryHighRate;
        var total = RoundMoney(lowTax + highTax);

        var steps = new List<ExplanationStep>
        {
            new("Supplemental wages this payment",
                "Bonuses, commissions, and other supplemental wages are withheld separately from regular pay under the flat-rate method.",
                wages,
                $"= {Money(wages)}"),
        };

        if (ytd > 0m)
        {
            steps.Add(new ExplanationStep(
                "Supplemental wages paid earlier this year",
                "The $1,000,000 threshold is cumulative for the calendar year, so prior supplemental wages count toward it.",
                ytd,
                $"= {Money(ytd)}"));
        }

        steps.Add(new ExplanationStep(
            $"Portion taxed at {FlatRate:P0}",
            $"Supplemental wages up to the cumulative {Money(MillionDollarThreshold)} threshold are withheld at the optional flat {FlatRate:P0} rate.",
            lowTax,
            $"{Money(lowPortion)} × {FlatRate:P0} = {Money(lowTax)}"));

        if (highPortion > 0m)
        {
            steps.Add(new ExplanationStep(
                $"Portion taxed at {MandatoryHighRate:P0}",
                $"Cumulative supplemental wages above {Money(MillionDollarThreshold)} must be withheld at the mandatory {MandatoryHighRate:P0} rate.",
                highTax,
                $"{Money(highPortion)} × {MandatoryHighRate:P0} = {Money(highTax)}"));
        }

        steps.Add(new ExplanationStep(
            "Federal supplemental withholding",
            "Total federal income tax withheld on the supplemental payment, rounded to the nearest cent.",
            total,
            $"= {Money(total)}"));

        var explanation = new LineExplanation(
            ExplanationLineKey.FederalWithholding,
            "Federal Withholding (Supplemental)",
            total,
            steps,
            "IRS Publication 15 (2026), Section 7 — flat-rate method for supplemental wages (22%; 37% over $1,000,000).");

        return (total, explanation);
    }

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
