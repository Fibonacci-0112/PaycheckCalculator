using PaycheckCalculator.Core.Explanation;
using PaycheckCalculator.Core.Models;
using PaycheckCalculator.Core.Tax.State;

namespace PaycheckCalculator.Core.Tax.NewYork;

/// <summary>
/// State module for New York (NY) income tax withholding.
/// Implements the annualized percentage-method formula described in
/// New York Publication NYS-50-T-NYS and Form IT-2104
/// (Employee's Withholding Allowance Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract the IT-2104 allowance deduction ($1,000 per allowance claimed).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply New York's 2026 graduated income tax brackets.
///      Head of Household uses the same brackets as Single.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form IT-2104.
///
/// Filing statuses (per Form IT-2104):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Widow(er).
///   • Head of Household — Head of Household.
///
/// 2026 New York amounts (NYS-50-T-NYS):
///   Standard deduction:
///     Single / MFS:          $8,000
///     Married / QW:          $16,050
///     Head of Household:     $11,000
///   Per-allowance deduction (IT-2104): $1,000 per allowance
///   Single and Head of Household brackets:
///     4.00%  on $0 – $8,500
///     4.50%  on $8,500 – $11,700
///     5.25%  on $11,700 – $13,900
///     5.90%  on $13,900 – $21,400
///     6.09%  on $21,400 – $80,650
///     6.41%  on $80,650 – $215,400
///     6.85%  on $215,400 – $1,077,550
///     9.65%  on $1,077,550 – $5,000,000
///     10.30% on $5,000,000 – $25,000,000
///     10.90% over $25,000,000
///   Married (MFJ / QW) brackets:
///     4.00%  on $0 – $17,150
///     4.50%  on $17,150 – $23,600
///     5.25%  on $23,600 – $27,900
///     5.90%  on $27,900 – $43,000
///     6.09%  on $43,000 – $161,550
///     6.41%  on $161,550 – $323,200
///     6.85%  on $323,200 – $2,155,350
///     9.65%  on $2,155,350 – $5,000,000
///     10.30% on $5,000,000 – $25,000,000
///     10.90% over $25,000,000
///
/// Sources:
///   • New York State Department of Taxation and Finance, Publication
///     NYS-50-T-NYS (New York State Withholding Tax Tables and Methods), 2026.
///   • Form IT-2104, Employee's Withholding Allowance Certificate.
/// </summary>
public sealed class NewYorkWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 NY standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 8_000m;

    /// <summary>2026 NY standard deduction for Married Filing Jointly / Qualifying Widow(er).</summary>
    public const decimal StandardDeductionMarried = 16_050m;

    /// <summary>2026 NY standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 11_000m;

    // ── Per-allowance deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per allowance claimed on Form IT-2104.
    /// New York uses $1,000 per allowance; this reduces annual taxable
    /// wages before the brackets are applied.
    /// </summary>
    public const decimal AllowanceAmount = 1_000m;

    // ── Bracket ceilings — Single and Head of Household ─────────────

    /// <summary>Upper ceiling of the first Single/HoH bracket ($8,500).</summary>
    public const decimal SingleBracket1Ceiling = 8_500m;

    /// <summary>Upper ceiling of the second Single/HoH bracket ($11,700).</summary>
    public const decimal SingleBracket2Ceiling = 11_700m;

    /// <summary>Upper ceiling of the third Single/HoH bracket ($13,900).</summary>
    public const decimal SingleBracket3Ceiling = 13_900m;

    /// <summary>Upper ceiling of the fourth Single/HoH bracket ($21,400).</summary>
    public const decimal SingleBracket4Ceiling = 21_400m;

    /// <summary>Upper ceiling of the fifth Single/HoH bracket ($80,650).</summary>
    public const decimal SingleBracket5Ceiling = 80_650m;

    /// <summary>Upper ceiling of the sixth Single/HoH bracket ($215,400).</summary>
    public const decimal SingleBracket6Ceiling = 215_400m;

    /// <summary>Upper ceiling of the seventh Single/HoH bracket ($1,077,550).</summary>
    public const decimal SingleBracket7Ceiling = 1_077_550m;

    /// <summary>Upper ceiling of the eighth Single/HoH bracket ($5,000,000).</summary>
    public const decimal SingleBracket8Ceiling = 5_000_000m;

    /// <summary>Upper ceiling of the ninth Single/HoH bracket ($25,000,000).</summary>
    public const decimal SingleBracket9Ceiling = 25_000_000m;

    // ── Bracket ceilings — Married (MFJ / QW) ───────────────────────

    /// <summary>Upper ceiling of the first Married bracket ($17,150).</summary>
    public const decimal MarriedBracket1Ceiling = 17_150m;

    /// <summary>Upper ceiling of the second Married bracket ($23,600).</summary>
    public const decimal MarriedBracket2Ceiling = 23_600m;

    /// <summary>Upper ceiling of the third Married bracket ($27,900).</summary>
    public const decimal MarriedBracket3Ceiling = 27_900m;

    /// <summary>Upper ceiling of the fourth Married bracket ($43,000).</summary>
    public const decimal MarriedBracket4Ceiling = 43_000m;

    /// <summary>Upper ceiling of the fifth Married bracket ($161,550).</summary>
    public const decimal MarriedBracket5Ceiling = 161_550m;

    /// <summary>Upper ceiling of the sixth Married bracket ($323,200).</summary>
    public const decimal MarriedBracket6Ceiling = 323_200m;

    /// <summary>Upper ceiling of the seventh Married bracket ($2,155,350).</summary>
    public const decimal MarriedBracket7Ceiling = 2_155_350m;

    /// <summary>Upper ceiling of the eighth Married bracket ($5,000,000).</summary>
    public const decimal MarriedBracket8Ceiling = 5_000_000m;

    /// <summary>Upper ceiling of the ninth Married bracket ($25,000,000).</summary>
    public const decimal MarriedBracket9Ceiling = 25_000_000m;

    // ── Tax rates (shared across all filing statuses) ────────────────

    /// <summary>New York rate for the first bracket (4.00%).</summary>
    public const decimal Rate1 = 0.04m;

    /// <summary>New York rate for the second bracket (4.50%).</summary>
    public const decimal Rate2 = 0.045m;

    /// <summary>New York rate for the third bracket (5.25%).</summary>
    public const decimal Rate3 = 0.0525m;

    /// <summary>New York rate for the fourth bracket (5.90%).</summary>
    public const decimal Rate4 = 0.059m;

    /// <summary>New York rate for the fifth bracket (6.09%).</summary>
    public const decimal Rate5 = 0.0609m;

    /// <summary>New York rate for the sixth bracket (6.41%).</summary>
    public const decimal Rate6 = 0.0641m;

    /// <summary>New York rate for the seventh bracket (6.85%).</summary>
    public const decimal Rate7 = 0.0685m;

    /// <summary>New York rate for the eighth bracket (9.65%).</summary>
    public const decimal Rate8 = 0.0965m;

    /// <summary>New York rate for the ninth bracket (10.30%).</summary>
    public const decimal Rate9 = 0.103m;

    /// <summary>New York top rate for the tenth bracket (10.90%).</summary>
    public const decimal Rate10 = 0.109m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public NewYorkWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.NY, "FilingStatus");
    }

public UsState State => UsState.NY;


    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");

        if (values.GetValueOrDefault("Allowances", 0) < 0)
            errors.Add("Allowances cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var allowances       = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        var steps = new List<ExplanationStep>();
        StateExplanationSteps.AddTaxableWagesSteps(steps, context, taxableWages);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;
        steps.Add(new ExplanationStep(
            $"Annualize wages ({periods} pay periods/year)",
            "New York's exact-calculation method estimates annual income from this period's wages.",
            annualWages,
            $"{StateExplanationSteps.Money(taxableWages)} × {periods} = {StateExplanationSteps.Money(annualWages)}"));

        // Step 3: Subtract the filing-status standard deduction.
        var standardDeduction = filingStatus switch
        {
            StatusMarried         => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _                     => StandardDeductionSingle
        };
        steps.Add(new ExplanationStep(
            $"Less standard deduction ({filingStatus})",
            "The IT-2104 filing status determines the annual standard deduction.",
            standardDeduction,
            $"− {StateExplanationSteps.Money(standardDeduction)}"));

        // Step 4: Subtract IT-2104 allowance deduction ($1,000 per allowance).
        var allowanceDeduction = allowances * AllowanceAmount;
        if (allowanceDeduction > 0m)
        {
            steps.Add(new ExplanationStep(
                "Less IT-2104 allowance deduction",
                $"Each allowance claimed on Form IT-2104 reduces annual income by {StateExplanationSteps.Money(AllowanceAmount)}.",
                allowanceDeduction,
                $"{allowances} × {StateExplanationSteps.Money(AllowanceAmount)} = {StateExplanationSteps.Money(allowanceDeduction)}"));
        }

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);
        steps.Add(new ExplanationStep(
            "Annual taxable income",
            "Annualized wages less the standard deduction and allowances, floored at zero.",
            annualTaxableIncome,
            $"max(0, {StateExplanationSteps.Money(annualWages)} − {StateExplanationSteps.Money(standardDeduction + allowanceDeduction)}) = {StateExplanationSteps.Money(annualTaxableIncome)}"));

        // Step 6: Apply NY's 2026 graduated brackets.
        //   Married (MFJ / QW) uses Married brackets.
        //   Single and Head of Household use the Single brackets.
        var annualTax = filingStatus == StatusMarried
            ? ApplyMarriedBrackets(annualTaxableIncome)
            : ApplySingleBrackets(annualTaxableIncome);

        if (annualTaxableIncome > 0m)
        {
            var (marginalRate, marginalFloor) = filingStatus == StatusMarried
                ? MarginalMarriedBracket(annualTaxableIncome)
                : MarginalSingleBracket(annualTaxableIncome);
            var scheduleName = filingStatus == StatusMarried ? "Married" : "Single / Head of Household";
            steps.Add(new ExplanationStep(
                $"Annual tax from New York's graduated brackets ({scheduleName} schedule)",
                $"Each slice of income is taxed at its bracket rate (4.00% in the lowest bracket up to 10.90% in the highest) and the slices are summed; the top slice is taxed at {StateExplanationSteps.Percent(marginalRate)} (income over {StateExplanationSteps.Money(marginalFloor)}).",
                annualTax,
                $"Tax on {StateExplanationSteps.Money(annualTaxableIncome)} = {StateExplanationSteps.Money(annualTax)}"));
        }

        // Step 7: De-annualize and round to two decimal places.
        var withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);
        steps.Add(new ExplanationStep(
            "De-annualize to this pay period",
            "Divide the annual tax back down to a per-period amount, rounded to the nearest cent.",
            withholding,
            $"{StateExplanationSteps.Money(annualTax)} ÷ {periods} = {StateExplanationSteps.Money(withholding)}"));

        // Step 8: Add any per-period extra withholding.
        withholding += extraWithholding;
        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "Form IT-2104");

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding  = withholding,
            WithholdingSteps = steps,
            WithholdingReference = "NYS Publication NYS-50-T-NYS (2026); Form IT-2104."
        };
    }

    /// <summary>Returns the rate and lower bound of the Single/HoH bracket the income's top dollar falls in (explanation only).</summary>
    private static (decimal Rate, decimal Floor) MarginalSingleBracket(decimal income) =>
        income > SingleBracket9Ceiling ? (Rate10, SingleBracket9Ceiling)
        : income > SingleBracket8Ceiling ? (Rate9, SingleBracket8Ceiling)
        : income > SingleBracket7Ceiling ? (Rate8, SingleBracket7Ceiling)
        : income > SingleBracket6Ceiling ? (Rate7, SingleBracket6Ceiling)
        : income > SingleBracket5Ceiling ? (Rate6, SingleBracket5Ceiling)
        : income > SingleBracket4Ceiling ? (Rate5, SingleBracket4Ceiling)
        : income > SingleBracket3Ceiling ? (Rate4, SingleBracket3Ceiling)
        : income > SingleBracket2Ceiling ? (Rate3, SingleBracket2Ceiling)
        : income > SingleBracket1Ceiling ? (Rate2, SingleBracket1Ceiling)
        : (Rate1, 0m);

    /// <summary>Returns the rate and lower bound of the Married bracket the income's top dollar falls in (explanation only).</summary>
    private static (decimal Rate, decimal Floor) MarginalMarriedBracket(decimal income) =>
        income > MarriedBracket9Ceiling ? (Rate10, MarriedBracket9Ceiling)
        : income > MarriedBracket8Ceiling ? (Rate9, MarriedBracket8Ceiling)
        : income > MarriedBracket7Ceiling ? (Rate8, MarriedBracket7Ceiling)
        : income > MarriedBracket6Ceiling ? (Rate7, MarriedBracket6Ceiling)
        : income > MarriedBracket5Ceiling ? (Rate6, MarriedBracket5Ceiling)
        : income > MarriedBracket4Ceiling ? (Rate5, MarriedBracket4Ceiling)
        : income > MarriedBracket3Ceiling ? (Rate4, MarriedBracket3Ceiling)
        : income > MarriedBracket2Ceiling ? (Rate3, MarriedBracket2Ceiling)
        : income > MarriedBracket1Ceiling ? (Rate2, MarriedBracket1Ceiling)
        : (Rate1, 0m);

    // ── Bracket helpers ───────────────────────────────────────────────

    /// <summary>
    /// Applies NY's Single / Head of Household brackets (2026 NYS-50-T-NYS).
    /// Head of Household uses these same brackets with its own standard deduction.
    /// </summary>
    private static decimal ApplySingleBrackets(decimal income)
    {
        // Single / HoH:
        //   4.00%  on $0 – $8,500
        //   4.50%  on $8,500 – $11,700
        //   5.25%  on $11,700 – $13,900
        //   5.90%  on $13,900 – $21,400
        //   6.09%  on $21,400 – $80,650
        //   6.41%  on $80,650 – $215,400
        //   6.85%  on $215,400 – $1,077,550
        //   9.65%  on $1,077,550 – $5,000,000
        //   10.30% on $5,000,000 – $25,000,000
        //   10.90% over $25,000,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, SingleBracket1Ceiling) * Rate1;

        if (income > SingleBracket1Ceiling)
            tax += (Math.Min(income, SingleBracket2Ceiling) - SingleBracket1Ceiling) * Rate2;

        if (income > SingleBracket2Ceiling)
            tax += (Math.Min(income, SingleBracket3Ceiling) - SingleBracket2Ceiling) * Rate3;

        if (income > SingleBracket3Ceiling)
            tax += (Math.Min(income, SingleBracket4Ceiling) - SingleBracket3Ceiling) * Rate4;

        if (income > SingleBracket4Ceiling)
            tax += (Math.Min(income, SingleBracket5Ceiling) - SingleBracket4Ceiling) * Rate5;

        if (income > SingleBracket5Ceiling)
            tax += (Math.Min(income, SingleBracket6Ceiling) - SingleBracket5Ceiling) * Rate6;

        if (income > SingleBracket6Ceiling)
            tax += (Math.Min(income, SingleBracket7Ceiling) - SingleBracket6Ceiling) * Rate7;

        if (income > SingleBracket7Ceiling)
            tax += (Math.Min(income, SingleBracket8Ceiling) - SingleBracket7Ceiling) * Rate8;

        if (income > SingleBracket8Ceiling)
            tax += (Math.Min(income, SingleBracket9Ceiling) - SingleBracket8Ceiling) * Rate9;

        if (income > SingleBracket9Ceiling)
            tax += (income - SingleBracket9Ceiling) * Rate10;

        return tax;
    }

    /// <summary>
    /// Applies NY's Married Filing Jointly / Qualifying Widow(er) brackets
    /// (2026 NYS-50-T-NYS).
    /// </summary>
    private static decimal ApplyMarriedBrackets(decimal income)
    {
        // Married (MFJ / QW):
        //   4.00%  on $0 – $17,150
        //   4.50%  on $17,150 – $23,600
        //   5.25%  on $23,600 – $27,900
        //   5.90%  on $27,900 – $43,000
        //   6.09%  on $43,000 – $161,550
        //   6.41%  on $161,550 – $323,200
        //   6.85%  on $323,200 – $2,155,350
        //   9.65%  on $2,155,350 – $5,000,000
        //   10.30% on $5,000,000 – $25,000,000
        //   10.90% over $25,000,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, MarriedBracket1Ceiling) * Rate1;

        if (income > MarriedBracket1Ceiling)
            tax += (Math.Min(income, MarriedBracket2Ceiling) - MarriedBracket1Ceiling) * Rate2;

        if (income > MarriedBracket2Ceiling)
            tax += (Math.Min(income, MarriedBracket3Ceiling) - MarriedBracket2Ceiling) * Rate3;

        if (income > MarriedBracket3Ceiling)
            tax += (Math.Min(income, MarriedBracket4Ceiling) - MarriedBracket3Ceiling) * Rate4;

        if (income > MarriedBracket4Ceiling)
            tax += (Math.Min(income, MarriedBracket5Ceiling) - MarriedBracket4Ceiling) * Rate5;

        if (income > MarriedBracket5Ceiling)
            tax += (Math.Min(income, MarriedBracket6Ceiling) - MarriedBracket5Ceiling) * Rate6;

        if (income > MarriedBracket6Ceiling)
            tax += (Math.Min(income, MarriedBracket7Ceiling) - MarriedBracket6Ceiling) * Rate7;

        if (income > MarriedBracket7Ceiling)
            tax += (Math.Min(income, MarriedBracket8Ceiling) - MarriedBracket7Ceiling) * Rate8;

        if (income > MarriedBracket8Ceiling)
            tax += (Math.Min(income, MarriedBracket9Ceiling) - MarriedBracket8Ceiling) * Rate9;

        if (income > MarriedBracket9Ceiling)
            tax += (income - MarriedBracket9Ceiling) * Rate10;

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly    => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
