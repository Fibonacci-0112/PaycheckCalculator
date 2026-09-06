using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Pay;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// One employee-paid state program — New Jersey's TDI, New York's PFL, Hawaii's
/// TDI — and the rule that caps it. See <see cref="StatePayrollAssessments"/>.
/// </summary>
public sealed class StatePayrollAssessment
{
    /// <summary>Display label for the paycheck line, e.g. "Family Leave Insurance (FLI)".</summary>
    public required string Label { get; init; }

    /// <summary>Compact program code used by exports and comparison rows.</summary>
    public required string ShortCode { get; init; }

    /// <summary>Employee contribution rate as a fraction, e.g. 0.0019 for 0.19%.</summary>
    public required decimal Rate { get; init; }

    /// <summary>Annual taxable wage base, when the program caps contributions by wages.</summary>
    public decimal? AnnualWageBase { get; init; }

    /// <summary>Statutory ceiling on the deduction per week, converted to the payroll frequency.</summary>
    public decimal? MaxWeeklyDeduction { get; init; }

    /// <summary>Ceiling on the employee's total contribution for the year.</summary>
    public decimal? MaxAnnualContribution { get; init; }

    /// <summary>Schema toggle that suppresses this program when the employee is exempt.</summary>
    public string? ExemptFieldKey { get; init; }

    /// <summary>Citation shown beneath the "Show Your Work" steps.</summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Computes this period's premium and the worksheet behind it. The base is
    /// gross wages before pre-tax deductions — these programs do not follow the
    /// taxable-wage reductions used for income tax.
    /// </summary>
    public StateTaxLine Compute(CommonWithholdingContext context)
    {
        var wages = Math.Max(0m, context.GrossWages);
        var ytd = Math.Max(0m, context.YtdStateWages);
        var steps = new List<ExplanationStep>
        {
            new("Gross wages this period",
                "The premium applies to gross wages, before any pre-tax deductions.",
                wages,
                $"= {Money(wages)}")
        };

        var taxable = wages;

        if (AnnualWageBase is { } wageBase)
        {
            // Same shape as the Social Security wage base: only the unused part of
            // the annual base is still subject to the premium.
            var remaining = Math.Max(0m, wageBase - ytd);
            taxable = Math.Min(wages, remaining);
            steps.Add(new ExplanationStep(
                "Remaining taxable wage base",
                $"{Label} applies only to the first {Money(wageBase)} of wages each year.",
                remaining,
                $"{Money(wageBase)} − {Money(ytd)} YTD = {Money(remaining)}"));
            steps.Add(new ExplanationStep(
                "Wages subject to the premium this period",
                "The lesser of this period's wages and the wage base still available.",
                taxable,
                $"min({Money(wages)}, {Money(remaining)}) = {Money(taxable)}"));
        }

        var amount = taxable * Rate;
        steps.Add(new ExplanationStep(
            $"{Label} ({Percent(Rate)})",
            "Mandatory employee contribution funding the state program.",
            Round(amount),
            $"{Money(taxable)} × {Percent(Rate)} = {Money(Round(amount))}"));

        if (MaxWeeklyDeduction is { } weeklyCap)
        {
            // The ceiling is statutory per week, so it scales with the payroll
            // frequency rather than being applied as written to every period.
            var weeksPerPeriod = 52m / PayPeriods.PerYear(context.PayPeriod);
            var periodCap = Round(weeklyCap * weeksPerPeriod);
            if (amount > periodCap)
            {
                steps.Add(new ExplanationStep(
                    "Maximum deduction for this pay period",
                    $"The deduction is capped at {Money(weeklyCap)} per week by statute.",
                    periodCap,
                    $"{Money(weeklyCap)} × {weeksPerPeriod:0.####} weeks = {Money(periodCap)}"));
                amount = periodCap;
            }
        }

        if (MaxAnnualContribution is { } annualCap)
        {
            // Contributions stop once the employee has paid the year's maximum, so
            // this period owes only the difference between the capped running total
            // and what year-to-date wages have already produced.
            var paidToDate = Math.Min(ytd * Rate, annualCap);
            var throughThisPeriod = Math.Min((ytd + wages) * Rate, annualCap);
            var capped = Math.Max(0m, throughThisPeriod - paidToDate);
            if (capped < amount)
            {
                steps.Add(new ExplanationStep(
                    "Annual contribution maximum",
                    $"Contributions stop once {Money(annualCap)} has been withheld for the year.",
                    Round(capped),
                    $"{Money(annualCap)} − {Money(Round(paidToDate))} already withheld = {Money(Round(capped))}"));
                amount = capped;
            }
        }

        return new StateTaxLine
        {
            Kind = StateTaxLineKind.PayrollAssessment,
            Label = Label,
            ShortCode = ShortCode,
            Amount = Round(amount),
            Steps = steps,
            Reference = Reference
        };
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Money(decimal value) => StateExplanationSteps.Money(value);
    private static string Percent(decimal rate) => StateExplanationSteps.Percent(rate);
}
