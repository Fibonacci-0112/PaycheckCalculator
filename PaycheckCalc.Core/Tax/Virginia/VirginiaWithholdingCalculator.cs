using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Virginia;

/// <summary>
/// State module for Virginia income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// Virginia Department of Taxation <em>Virginia Employer Withholding
/// Instructions</em> (Publication 93045, 2026).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the standard deduction for the VA-4 filing status
///      ($8,750 for Single; $17,500 for Married or Head of Household).
///   4. Subtract personal exemptions (exemptions × $930).
///   5. Low-income exemption: if the resulting annual taxable income is
///      zero or negative, no income tax is withheld.
///   6. Apply Virginia's graduated income tax brackets to annual taxable income.
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form VA-4.
///
/// Filing statuses (per Form VA-4):
///   • Single           — Single or Married Filing Separately (higher withholding rate).
///   • Married          — Married Filing Jointly or Qualifying Widow(er).
///   • Head of Household — Head of Household (uses Married standard deduction).
///
/// 2026 Virginia amounts (Virginia Dept. of Taxation, Pub. 93045, 2026):
///   Standard deduction: $8,750 (Single); $17,500 (Married / Head of Household)
///   Per-exemption deduction: $930 (claimed on Form VA-4)
///   Brackets (all filing statuses share the same bracket thresholds):
///     2.00% on $0 – $3,000
///     3.00% on $3,001 – $5,000
///     5.00% on $5,001 – $17,000
///     5.75% over $17,000
///
/// Sources:
///   • Virginia Department of Taxation, <em>Virginia Employer Withholding
///     Instructions</em> (Publication 93045), effective January 1, 2026.
///   • Virginia Code § 58.1-322.01 (standard deductions); § 58.1-322.02 (exemptions).
/// </summary>
public sealed class VirginiaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>Annual standard deduction for Single (and Married Filing Separately) filers (2026).</summary>
    public const decimal StandardDeductionSingle = 8_750m;

    /// <summary>
    /// Annual standard deduction for Married Filing Jointly and Head of Household filers (2026).
    /// Head of Household uses the same $17,500 standard deduction as Married.
    /// </summary>
    public const decimal StandardDeductionMarriedOrHoh = 17_500m;

    // ── Exemption ────────────────────────────────────────────────────

    /// <summary>Annual deduction per personal exemption claimed on Form VA-4 (2026).</summary>
    public const decimal ExemptionAmount = 930m;

    // ── Bracket thresholds (all filing statuses use the same brackets) ──

    /// <summary>Upper bound of the first (2.00%) bracket.</summary>
    public const decimal Bracket1Ceiling = 3_000m;

    /// <summary>Upper bound of the second (3.00%) bracket.</summary>
    public const decimal Bracket2Ceiling = 5_000m;

    /// <summary>Upper bound of the third (5.00%) bracket.</summary>
    public const decimal Bracket3Ceiling = 17_000m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>Virginia income tax rate for the first bracket (2.00%).</summary>
    public const decimal Rate1 = 0.02m;

    /// <summary>Virginia income tax rate for the second bracket (3.00%).</summary>
    public const decimal Rate2 = 0.03m;

    /// <summary>Virginia income tax rate for the third bracket (5.00%).</summary>
    public const decimal Rate3 = 0.05m;

    /// <summary>Virginia income tax rate for the top bracket (5.75%).</summary>
    public const decimal Rate4 = 0.0575m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";


    // ── Schema ───────────────────────────────────────────────────────


    // ── IStateWithholdingCalculator ──────────────────────────────────

        private readonly IReadOnlyList<string> _filingStatusOptions;

    public VirginiaWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.VA, "FilingStatus");
    }

public UsState State => UsState.VA;


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

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var exemptions = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
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
            "Virginia's percentage method estimates annual income from this period's wages.",
            annualWages,
            $"{StateExplanationSteps.Money(taxableWages)} × {periods} = {StateExplanationSteps.Money(annualWages)}"));

        // Step 3: Subtract standard deduction based on VA-4 filing status.
        // Head of Household uses the same $17,500 deduction as Married.
        var standardDeduction = filingStatus == StatusSingle
            ? StandardDeductionSingle
            : StandardDeductionMarriedOrHoh;
        annualWages -= standardDeduction;
        steps.Add(new ExplanationStep(
            $"Less standard deduction ({filingStatus})",
            "The VA-4 filing status determines the annual standard deduction; Head of Household uses the Married amount.",
            standardDeduction,
            $"− {StateExplanationSteps.Money(standardDeduction)}"));

        // Step 4: Subtract personal exemptions ($930 each).
        var exemptionDeduction = exemptions * ExemptionAmount;
        annualWages -= exemptionDeduction;
        if (exemptionDeduction > 0m)
        {
            steps.Add(new ExplanationStep(
                "Less personal exemptions (VA-4)",
                $"Each exemption claimed on Form VA-4 reduces annual income by {StateExplanationSteps.Money(ExemptionAmount)}.",
                exemptionDeduction,
                $"{exemptions} × {StateExplanationSteps.Money(ExemptionAmount)} = {StateExplanationSteps.Money(exemptionDeduction)}"));
        }

        // Step 5: Low-income exemption — floor annual taxable at zero.
        var annualTaxableIncome = Math.Max(0m, annualWages);
        steps.Add(new ExplanationStep(
            "Annual taxable income",
            "Annualized wages less deductions and exemptions, floored at zero. When this is zero, Virginia withholds nothing.",
            annualTaxableIncome,
            $"= {StateExplanationSteps.Money(annualTaxableIncome)}"));

        // Step 6: Apply graduated brackets (all statuses share the same thresholds).
        var annualTax = ApplyBrackets(annualTaxableIncome);
        if (annualTaxableIncome > 0m)
        {
            var (marginalRate, marginalFloor) = MarginalBracket(annualTaxableIncome);
            steps.Add(new ExplanationStep(
                "Annual tax from Virginia's graduated brackets",
                $"Each slice of income is taxed at its bracket rate (2% to $3,000, 3% to $5,000, 5% to $17,000, 5.75% above) and the slices are summed; the top slice is taxed at {StateExplanationSteps.Percent(marginalRate)} (income over {StateExplanationSteps.Money(marginalFloor)}).",
                annualTax,
                $"Tax on {StateExplanationSteps.Money(annualTaxableIncome)} = {StateExplanationSteps.Money(annualTax)}"));
        }

        // Step 7: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);
        steps.Add(new ExplanationStep(
            "De-annualize to this pay period",
            "Divide the annual tax back down to a per-period amount, rounded to the nearest cent.",
            withholding,
            $"{StateExplanationSteps.Money(annualTax)} ÷ {periods} = {StateExplanationSteps.Money(withholding)}"));

        // Step 8: Add any per-period extra withholding.
        withholding += extraWithholding;
        StateExplanationSteps.AddExtraWithholdingStep(steps, extraWithholding, withholding, "Form VA-4");

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            WithholdingSteps = steps,
            WithholdingReference = "Virginia Dept. of Taxation, Employer Withholding Instructions (Pub. 93045, 2026)."
        };
    }

    /// <summary>Returns the rate and lower bound of the bracket the income's top dollar falls in (explanation only).</summary>
    private static (decimal Rate, decimal Floor) MarginalBracket(decimal income) =>
        income > Bracket3Ceiling ? (Rate4, Bracket3Ceiling)
        : income > Bracket2Ceiling ? (Rate3, Bracket2Ceiling)
        : income > Bracket1Ceiling ? (Rate2, Bracket1Ceiling)
        : (Rate1, 0m);

    // ── Bracket helper ────────────────────────────────────────────────

    private static decimal ApplyBrackets(decimal income)
    {
        // Virginia graduated brackets (identical for all filing statuses):
        //   2.00% on $0 – $3,000
        //   3.00% on $3,001 – $5,000
        //   5.00% on $5,001 – $17,000
        //   5.75% over $17,000
        if (income <= 0m)
            return 0m;

        decimal tax = 0m;

        var tier1 = Math.Min(income, Bracket1Ceiling);
        tax += tier1 * Rate1;

        if (income > Bracket1Ceiling)
        {
            var tier2 = Math.Min(income, Bracket2Ceiling) - Bracket1Ceiling;
            tax += tier2 * Rate2;
        }

        if (income > Bracket2Ceiling)
        {
            var tier3 = Math.Min(income, Bracket3Ceiling) - Bracket2Ceiling;
            tax += tier3 * Rate3;
        }

        if (income > Bracket3Ceiling)
        {
            var tier4 = income - Bracket3Ceiling;
            tax += tier4 * Rate4;
        }

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => 260,
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
