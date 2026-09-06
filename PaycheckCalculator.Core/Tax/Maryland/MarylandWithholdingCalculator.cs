using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.Maryland;

/// <summary>
/// State module for Maryland (MD) income tax withholding.
/// Implements the percentage method of the Maryland Employer Withholding Guide
/// (Comptroller of Maryland, revised December 2025, effective January 2026).
///
/// Maryland's percentage method works on <b>one payroll period at a time</b> — it
/// never annualizes. The guide states the formula (page 10) as:
///
///   Total wages (before any deductions)
///   LESS  Allowance for Standard Deduction (for the payroll period)
///   LESS  Value of exemptions (number of exemptions × the amount for one
///         exemption for the payroll period)
///   Equals TAXABLE INCOME
///
/// then applies a combined state-plus-local percentage table to that figure.
/// Because every published table is the state rate schedule plus a flat local
/// rate on the same taxable income, this calculator computes the two components
/// directly — which also lets it use each county's <i>actual</i> rate rather than
/// the rounded-up rate group the printed tables are bucketed into.
///
/// Calculation steps:
///   1. Per-period state taxable wages (gross − pre-tax deductions that reduce
///      state wages, floored at $0).
///   2. If those wages fall below the payroll period's "DO NOT WITHHOLD ON GROSS
///      WAGES LESS THAN" threshold, nothing is withheld — state or county.
///   3. Subtract the payroll period's standard deduction allowance.
///   4. Subtract the MW507 exemptions (count × the period's exemption amount).
///   5. Apply the state withholding rate schedule to the resulting taxable
///      income, with the period's bracket thresholds.
///   6. Apply the county rate to that same taxable income.
///   7. Round each line to two decimal places, then add any additional
///      per-period withholding the employee requested on Form MW507.
///
/// Two rules of the withholding schedule differ from the annual return:
///
///   • <b>4.75% minimum rate.</b> "Maryland law does not permit the use of a rate
///     of less than 4.75% to be used for withholding tax purposes"
///     (Withholding Tax Facts 2026), so the 2%/3%/4% brackets that apply on Form
///     502 are not used here — withholding starts at 4.75% from the first dollar.
///   • <b>Flat standard deduction.</b> "The standard deduction amounts have
///     changed per new legislation enacted in the 2025 Legislative Session. For
///     the purpose of the percentage method calculation the Standard Deduction is
///     $3,400" (guide page 2) — it no longer varies with wages or filing status.
///
/// Filing statuses (per Form MW507), matching the guide's two rate columns:
///   • Single           — single filers, married filing separately, dependents.
///   • Married          — married filing jointly.
///   • Head of Household — the guide's JOINT column also covers head of
///                         household and qualifying surviving spouse.
///
/// 2026 state withholding rate schedule (annual taxable income):
///   Single / MFS / dependent:
///     4.75% on $0 – $100,000
///     5.00% on $100,000 – $125,000
///     5.25% on $125,000 – $150,000
///     5.50% on $150,000 – $250,000
///     5.75% on $250,000 – $500,000
///     6.25% on $500,000 – $1,000,000
///     6.50% over $1,000,000
///   Married filing jointly / Head of Household / qualifying surviving spouse:
///     4.75% on $0 – $150,000
///     5.00% on $150,000 – $175,000
///     5.25% on $175,000 – $225,000
///     5.50% on $225,000 – $300,000
///     5.75% on $300,000 – $600,000
///     6.25% on $600,000 – $1,200,000
///     6.50% over $1,200,000
///
/// Worked example (matching the guide's 3.05% tables, page 31): a single
/// employee paid $1,600 bi-weekly in Carroll County (3.03%) with no exemptions —
///   taxable income = $1,600 − $130.76 = $1,469.24
///   state          = $1,469.24 × 4.75% = $69.79
///   county         = $1,469.24 × 3.03% = $44.52
/// The printed table reaches the same total through its combined 7.80% rate
/// (4.75% state + the 3.05% rate group Carroll's 3.03% rounds up into).
///
/// Sources:
///   • Comptroller of Maryland, <em>Maryland Employer Withholding Guide</em>,
///     effective January 2026 (revised December 2025) — percentage method,
///     pages 6 and 10, and the ten local-rate tables on pages 13–42.
///   • Comptroller of Maryland, <em>Withholding Tax Facts</em>,
///     January 2026 – December 2026 — state rate schedules and county rates.
/// </summary>
public sealed class MarylandWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Percentage-method allowances (annual basis) ───────────────────

    /// <summary>
    /// Annual standard deduction used by the percentage method. Set by the 2025
    /// Legislative Session; flat, so it no longer varies with wages or status.
    /// </summary>
    public const decimal StandardDeductionAnnual = 3_400m;

    /// <summary>Annual value of one MW507 personal exemption.</summary>
    public const decimal ExemptionAnnual = 3_200m;

    /// <summary>
    /// The lowest rate Maryland law permits for withholding. It replaces the
    /// 2%/3%/4% brackets of the annual return, so withholding starts here.
    /// </summary>
    public const decimal MinimumWithholdingRate = 0.0475m;

    /// <summary>
    /// Annual wages below which no Maryland tax is withheld, matching the
    /// "DO NOT WITHHOLD ON GROSS WAGES LESS THAN" line on every published table.
    /// </summary>
    public const decimal NoWithholdingBelowAnnual = 5_000m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    // ── State withholding rate schedules ─────────────────────────────
    //
    // Annual bracket ceilings, scaled down to the payroll period before use.
    // The first band carries the 4.75% statutory minimum rather than the
    // 2%/3%/4% bands of the annual return.

    private static readonly (decimal AnnualCeiling, decimal Rate)[] SingleSchedule =
    [
        (  100_000m, MinimumWithholdingRate),
        (  125_000m, 0.05m),
        (  150_000m, 0.0525m),
        (  250_000m, 0.055m),
        (  500_000m, 0.0575m),
        (1_000_000m, 0.0625m),
        (decimal.MaxValue, 0.065m)
    ];

    private static readonly (decimal AnnualCeiling, decimal Rate)[] JointSchedule =
    [
        (  150_000m, MinimumWithholdingRate),
        (  175_000m, 0.05m),
        (  225_000m, 0.0525m),
        (  300_000m, 0.055m),
        (  600_000m, 0.0575m),
        (1_200_000m, 0.0625m),
        (decimal.MaxValue, 0.065m)
    ];

    // ── Per-payroll-period allowances ────────────────────────────────

    /// <summary>
    /// The allowances the guide publishes for one payroll period.
    /// </summary>
    /// <param name="StandardDeduction">The period's share of the $3,400 standard deduction.</param>
    /// <param name="Exemption">The period's value of one MW507 exemption.</param>
    /// <param name="NoWithholdingBelow">Wages below which the tables withhold nothing.</param>
    /// <param name="BracketDivisor">Divisor that scales the annual bracket ceilings to this period.</param>
    private readonly record struct PeriodAllowances(
        decimal StandardDeduction,
        decimal Exemption,
        decimal NoWithholdingBelow,
        int BracketDivisor);

    /// <summary>
    /// Guide page 10 publishes these amounts verbatim; they are used as printed
    /// rather than recomputed, so results match the tables to the cent.
    ///
    /// The daily row is the guide's own inconsistency, reproduced deliberately:
    /// its allowances are the annual figures over 365 days ($3,400 ÷ 365 = $9.31,
    /// $3,200 ÷ 365 = $8.77), while the bracket thresholds in its daily tables
    /// are the annual figures over 364 (52 weeks × 7 days) — $100,000 ÷ 364 =
    /// $275, as printed on page 31.
    /// </summary>
    private static PeriodAllowances AllowancesFor(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily        => new(     9.31m,     8.77m,    13.70m, 364),
        PayFrequency.Weekly       => new(    65.38m,    61.54m,    96.00m,  52),
        PayFrequency.Weekly53     => new(    65.38m,    61.54m,    96.00m,  52),
        PayFrequency.Biweekly     => new(   130.76m,   123.08m,   192.00m,  26),
        PayFrequency.Biweekly27   => new(   130.76m,   123.08m,   192.00m,  26),
        PayFrequency.Semimonthly  => new(   141.66m,   133.33m,   208.00m,  24),
        PayFrequency.Monthly      => new(   283.33m,   266.67m,   417.00m,  12),
        PayFrequency.Quarterly    => new(   850.00m,   800.00m, 1_250.00m,   4),

        // The guide publishes no semiannual table, so this row is derived from
        // the annual amounts. Every other row is taken from the guide as printed.
        PayFrequency.Semiannual   => new( 1_700.00m, 1_600.00m, 2_500.00m,   2),

        PayFrequency.Annual       => new( 3_400.00m, 3_200.00m, 5_000.00m,   1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;
    private readonly IReadOnlyList<string> _countyOptions;
    private readonly MarylandCountyRates _countyRates;

    public MarylandWithholdingCalculator(IStateSchemaProvider schemaProvider, MarylandCountyRates countyRates)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.MD, "FilingStatus");
        _countyOptions = schemaProvider.GetOptions(UsState.MD, "County");
        _countyRates = countyRates;
    }

    public UsState State => UsState.MD;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Exemptions", 0) < 0)
            errors.Add("Exemptions cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        var county = values.GetValueOrDefault<string>("County", "");
        if (_countyOptions.Count > 0 && !_countyOptions.Contains(county))
            errors.Add($"County must be one of: {string.Join(", ", _countyOptions)}.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var exemptions       = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        var allowances = AllowancesFor(context.PayPeriod);

        bool isMarriedOrHoH = filingStatus == StatusMarried
                           || filingStatus == StatusHeadOfHousehold;

        // Step 2: Below the period's floor the tables withhold nothing at all —
        // neither state nor county — so the allowances never come into play.
        bool belowFloor = taxableWages < allowances.NoWithholdingBelow;

        // Steps 3-4: Subtract the standard deduction and the MW507 exemptions.
        var exemptionDeduction = exemptions * allowances.Exemption;
        var taxableIncome = belowFloor
            ? 0m
            : Math.Max(0m, taxableWages - allowances.StandardDeduction - exemptionDeduction);

        // Step 5: State withholding rate schedule, at the 4.75% minimum rate.
        var stateTax = ApplySchedule(
            isMarriedOrHoH ? JointSchedule : SingleSchedule,
            taxableIncome,
            allowances.BracketDivisor);

        // Step 6: County income tax. Every Maryland employee pays one, on the same
        // taxable income the state schedule uses; the Comptroller's own tables fold
        // it into a combined rate, but it is shown here as its own line.
        var county = _countyRates.Calculate(
            values.GetValueOrDefault<string>("County", _countyRates.DefaultCounty),
            taxableIncome,
            allowances.BracketDivisor,
            isMarriedOrHoH);

        // Step 7: Round each line on its own, then add the per-period extra
        // withholding the employee elected. Rounding the two lines separately is
        // why their sum can sit a cent off a printed table's combined figure.
        var withholding = Math.Round(stateTax, 2, MidpointRounding.AwayFromZero) + extraWithholding;
        var countyWithholding = Math.Round(county.Tax, 2, MidpointRounding.AwayFromZero);

        var lines = new List<StateTaxLine>
        {
            new()
            {
                Kind = StateTaxLineKind.StateIncome,
                Label = StateTaxLineResolver.StateIncomeLabel,
                Amount = withholding,
                Steps = BuildStateSteps(
                    context, taxableWages, allowances, belowFloor, exemptions,
                    exemptionDeduction, taxableIncome, stateTax, extraWithholding, withholding),
                Reference = "Comptroller of Maryland, 2026 Employer Withholding Guide — percentage method (page 10)."
            },
            new()
            {
                Kind = StateTaxLineKind.CountyIncome,
                Label = $"County Income Tax ({county.CountyName})",
                ShortCode = "County",
                Amount = countyWithholding,
                Steps = BuildCountySteps(county, belowFloor, allowances),
                Reference = "Comptroller of Maryland, Withholding Tax Facts January 2026 – December 2026 — county rates."
            }
        };

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            TaxLines = lines
        };
    }

    private static IReadOnlyList<ExplanationStep> BuildStateSteps(
        CommonWithholdingContext context,
        decimal taxableWages,
        PeriodAllowances allowances,
        bool belowFloor,
        int exemptions,
        decimal exemptionDeduction,
        decimal taxableIncome,
        decimal stateTax,
        decimal extraWithholding,
        decimal withholding)
    {
        var steps = new List<ExplanationStep>();
        StateExplanationSteps.AddTaxableWagesSteps(steps, context, taxableWages);

        if (belowFloor)
        {
            steps.Add(new ExplanationStep(
                "Below the withholding floor",
                "Maryland's tables withhold nothing on wages under this amount for the payroll period.",
                0m,
                $"{StateExplanationSteps.Money(taxableWages)} < {StateExplanationSteps.Money(allowances.NoWithholdingBelow)} = {StateExplanationSteps.Money(0m)}"));
            StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "Form MW507");
            return steps;
        }

        steps.Add(new ExplanationStep(
            "Less standard deduction",
            "Maryland's percentage method subtracts one payroll period's share of the $3,400 standard deduction.",
            allowances.StandardDeduction,
            $"− {StateExplanationSteps.Money(allowances.StandardDeduction)}"));

        if (exemptions > 0)
        {
            steps.Add(new ExplanationStep(
                "Less MW507 exemptions",
                $"{exemptions} exemption(s) at {StateExplanationSteps.Money(allowances.Exemption)} each for this payroll period.",
                exemptionDeduction,
                $"{exemptions} × {StateExplanationSteps.Money(allowances.Exemption)} = − {StateExplanationSteps.Money(exemptionDeduction)}"));
        }

        steps.Add(new ExplanationStep(
            "Taxable income this period",
            "The base for both the state rate schedule and the county rate.",
            taxableIncome,
            $"= {StateExplanationSteps.Money(taxableIncome)}"));

        var rounded = Math.Round(stateTax, 2, MidpointRounding.AwayFromZero);
        steps.Add(new ExplanationStep(
            "State income tax",
            "Maryland withholding starts at 4.75%; state law does not permit a lower rate for withholding.",
            rounded,
            $"= {StateExplanationSteps.Money(rounded)}"));

        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "Form MW507");
        return steps;
    }

    private static IReadOnlyList<ExplanationStep> BuildCountySteps(
        MarylandCountyTax county,
        bool belowFloor,
        PeriodAllowances allowances)
    {
        if (belowFloor)
        {
            return
            [
                new ExplanationStep(
                    "Below the withholding floor",
                    "No county tax is withheld either when wages fall under the payroll period's threshold.",
                    0m,
                    $"< {StateExplanationSteps.Money(allowances.NoWithholdingBelow)} = {StateExplanationSteps.Money(0m)}")
            ];
        }

        return [.. county.Steps];
    }

    // ── Bracket helper ────────────────────────────────────────────────

    /// <summary>
    /// Applies a rate schedule to one payroll period's taxable income, scaling
    /// each annual bracket ceiling down to the period. Brackets are marginal.
    /// </summary>
    private static decimal ApplySchedule(
        (decimal AnnualCeiling, decimal Rate)[] schedule, decimal taxableIncome, int bracketDivisor)
    {
        if (taxableIncome <= 0m) return 0m;

        decimal tax = 0m;
        decimal lower = 0m;

        foreach (var (annualCeiling, rate) in schedule)
        {
            if (taxableIncome <= lower)
                break;

            var upper = annualCeiling == decimal.MaxValue
                ? decimal.MaxValue
                : annualCeiling / bracketDivisor;

            tax += (Math.Min(taxableIncome, upper) - lower) * rate;
            lower = upper;
        }

        return tax;
    }
}
