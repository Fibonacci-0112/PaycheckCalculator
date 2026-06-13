using System.Text.Json;
using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal;

/// <summary>
/// IRS Publication 15-T (2026) - Section 1 (Automated Payroll Systems), Worksheet 1A + Annual Percentage Method tables.
/// </summary>
public sealed class Irs15TPercentageCalculator
{
    private readonly Irs15TRoot _data;

    public Irs15TPercentageCalculator(string json)
    {
        json = json.Replace("None", "null");
        _data = JsonSerializer.Deserialize<Irs15TRoot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to load IRS 15-T JSON data.");
    }

    public decimal CalculateWithholding(
        decimal taxableWagesThisPeriod,
        PayFrequency frequency,
        FederalW4Input w4)
        => CalculateWithExplanation(taxableWagesThisPeriod, frequency, w4).Withholding;

    /// <summary>
    /// Computes federal withholding and also produces a step-by-step
    /// <see cref="LineExplanation"/> mirroring IRS Pub 15-T Worksheet 1A so the UI
    /// can show how the number was reached.
    /// </summary>
    public (decimal Withholding, LineExplanation Explanation) CalculateWithExplanation(
        decimal taxableWagesThisPeriod,
        PayFrequency frequency,
        FederalW4Input w4)
    {
        var steps = new List<ExplanationStep>();

        steps.Add(new ExplanationStep(
            "Federal taxable wages this period",
            "Gross wages reduced by pre-tax deductions that lower federal taxable income (e.g. traditional 401(k), Section 125 medical).",
            taxableWagesThisPeriod,
            $"= {Money(taxableWagesThisPeriod)}"));

        if (taxableWagesThisPeriod <= 0m)
        {
            steps.Add(new ExplanationStep(
                "No withholding",
                "Federal taxable wages are zero, so no federal income tax is withheld this period.",
                0m));
            return (0m, BuildExplanation(0m, steps));
        }

        var payPeriods = PayPeriodsPerYear(frequency);

        // Worksheet 1A, Step 1 (annualize wages)
        var annualWage = taxableWagesThisPeriod * payPeriods;
        steps.Add(new ExplanationStep(
            $"Step 1a — Annualize wages ({payPeriods:0} pay periods/year)",
            "Multiply this period's taxable wages by the number of pay periods in a year to estimate annual income.",
            annualWage,
            $"{Money(taxableWagesThisPeriod)} × {payPeriods:0} = {Money(annualWage)}"));

        // Worksheet 1A (2020+ W-4 branch): 1e
        var line1e = annualWage + w4.Step4aOtherIncome;
        if (w4.Step4aOtherIncome != 0m)
        {
            steps.Add(new ExplanationStep(
                "Step 1e — Add W-4 Step 4(a) other income",
                "Add any non-job income reported on Form W-4 Step 4(a) (e.g. interest, dividends, retirement income).",
                line1e,
                $"{Money(annualWage)} + {Money(w4.Step4aOtherIncome)} = {Money(line1e)}"));
        }

        // Worksheet 1A: 1g standard deduction
        var line1g = w4.Step2Checked
            ? 0m
            : (w4.FilingStatus == FederalFilingStatus.MarriedFilingJointly
                ? _data.WorksheetConstants.Line1G.Mfj
                : _data.WorksheetConstants.Line1G.Other);
        steps.Add(new ExplanationStep(
            "Step 1g — Standard deduction",
            w4.Step2Checked
                ? "W-4 Step 2 (multiple jobs / spouse works) is checked, so the worksheet uses $0 for the built-in standard deduction."
                : (w4.FilingStatus == FederalFilingStatus.MarriedFilingJointly
                    ? "Married Filing Jointly uses the larger built-in deduction amount."
                    : "Single / Head of Household / Married Filing Separately use the smaller built-in deduction amount."),
            line1g,
            $"= {Money(line1g)}"));

        var line1h = w4.Step4bDeductions + line1g;
        if (w4.Step4bDeductions != 0m)
        {
            steps.Add(new ExplanationStep(
                "Step 1h — Add W-4 Step 4(b) deductions",
                "Add itemized or other deductions the employee claimed on Form W-4 Step 4(b).",
                line1h,
                $"{Money(line1g)} + {Money(w4.Step4bDeductions)} = {Money(line1h)}"));
        }

        var adjustedAnnualWage = Math.Max(0m, line1e - line1h);
        steps.Add(new ExplanationStep(
            "Adjusted annual wage amount",
            "Subtract total deductions from annual income (floored at zero). This is the income the tax tables are applied to.",
            adjustedAnnualWage,
            $"max(0, {Money(line1e)} − {Money(line1h)}) = {Money(adjustedAnnualWage)}"));

        if (adjustedAnnualWage <= 0m)
        {
            steps.Add(new ExplanationStep(
                "No withholding",
                "Adjusted annual wage is zero after deductions, so no federal income tax is withheld this period.",
                0m));
            return (0m, BuildExplanation(0m, steps));
        }

        // Worksheet 1A, Step 2 (Annual % method table)
        var schedule = w4.Step2Checked ? _data.AnnualTables.Step2Checked : _data.AnnualTables.Standard;
        var brackets = GetBrackets(schedule, w4.FilingStatus);
        var b = FindBracket(brackets, adjustedAnnualWage);

        var bracketBound = b.Under is null ? "∞" : Money(b.Under.Value);
        steps.Add(new ExplanationStep(
            "Step 2 — Locate the annual tax bracket",
            $"Look up {Money(adjustedAnnualWage)} in the {(w4.Step2Checked ? "Step 2 checked" : "standard")} {FilingStatusName(w4.FilingStatus)} table.",
            b.Rate,
            $"Bracket: {Money(b.Over)}–{bracketBound}, base {Money(b.Base)} + {b.Rate:P2} of excess over {Money(b.ExcessOver)}"));

        var tentativeAnnual = b.Base + (adjustedAnnualWage - b.ExcessOver) * b.Rate;
        steps.Add(new ExplanationStep(
            "Step 2 — Tentative annual withholding",
            "Apply the bracket: base amount + (income above the bracket floor × bracket rate).",
            tentativeAnnual,
            $"{Money(b.Base)} + ({Money(adjustedAnnualWage)} − {Money(b.ExcessOver)}) × {b.Rate:P2} = {Money(tentativeAnnual)}"));

        var tentativePerPeriod = tentativeAnnual / payPeriods;
        steps.Add(new ExplanationStep(
            "De-annualize to this pay period",
            "Divide the annual tax back down to a per-period amount.",
            tentativePerPeriod,
            $"{Money(tentativeAnnual)} ÷ {payPeriods:0} = {Money(tentativePerPeriod)}"));

        // Worksheet 1A, Step 3 (tax credits)
        var creditsPerPeriod = w4.Step3TaxCredits / payPeriods;
        var afterCredits = Math.Max(0m, tentativePerPeriod - creditsPerPeriod);
        if (w4.Step3TaxCredits != 0m)
        {
            steps.Add(new ExplanationStep(
                "Step 3 — Subtract W-4 Step 3 tax credits",
                "Spread W-4 Step 3 credits evenly across the year and subtract from this period's tax.",
                afterCredits,
                $"max(0, {Money(tentativePerPeriod)} − {Money(creditsPerPeriod)}) = {Money(afterCredits)}"));
        }

        // Worksheet 1A, Step 4 (extra withholding per pay period)
        var final = afterCredits + w4.Step4cExtraWithholding;
        if (w4.Step4cExtraWithholding != 0m)
        {
            steps.Add(new ExplanationStep(
                "Step 4(c) — Add extra withholding",
                "Add the per-period extra amount the employee requested on Form W-4 Step 4(c).",
                final,
                $"{Money(afterCredits)} + {Money(w4.Step4cExtraWithholding)} = {Money(final)}"));
        }

        var rounded = RoundMoney(final);
        steps.Add(new ExplanationStep(
            "Federal withholding (rounded)",
            "Rounded to the nearest cent, away from zero.",
            rounded,
            $"= {Money(rounded)}"));

        return (rounded, BuildExplanation(rounded, steps));
    }

    private static LineExplanation BuildExplanation(decimal final, List<ExplanationStep> steps)
        => new(
            ExplanationLineKey.FederalWithholding,
            "Federal Withholding",
            final,
            steps,
            "IRS Publication 15-T (2026), Worksheet 1A — Automated Payroll Systems");

    private static string FilingStatusName(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => "Married Filing Jointly",
        FederalFilingStatus.HeadOfHousehold => "Head of Household",
        _ => "Single / Married Filing Separately"
    };

    private static string Money(decimal v) => v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static List<AnnualBracket> GetBrackets(AnnualSchedule schedule, FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => schedule.MarriedFilingJointly,
        FederalFilingStatus.HeadOfHousehold => schedule.HeadOfHousehold,
        _ => schedule.SingleOrMfs
    };

    private static AnnualBracket FindBracket(List<AnnualBracket> brackets, decimal wages)
    {
        foreach (var b in brackets)
        {
            var underOk = b.Under is null || wages < b.Under.Value;
            if (wages >= b.Over && underOk) return b;
        }
        return brackets.Last();
    }

    // Pub 15-T rounding convention (nearest whole dollar; <50c down, >=50c up)
    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static decimal PayPeriodsPerYear(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52m,
        PayFrequency.Biweekly => 26m,
        PayFrequency.Semimonthly => 24m,
        PayFrequency.Monthly => 12m,
        PayFrequency.Quarterly => 4m,
        PayFrequency.Semiannual => 2m,
        PayFrequency.Annual => 1m,
        PayFrequency.Daily => 260m,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
