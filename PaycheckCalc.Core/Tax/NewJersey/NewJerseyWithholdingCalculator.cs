using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.NewJersey;

/// <summary>
/// State module for New Jersey (NJ) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// New Jersey Division of Taxation publication NJ-WT and Form NJ-W4
/// (Employee's Withholding Allowance Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the per-allowance deduction ($1,000 per allowance claimed
///      on Form NJ-W4) to get annual taxable income.
///   4. Low-income exemption: floor annual taxable income at zero.
///   5. Apply New Jersey's 2026 graduated income tax brackets.
///      Filing status determines which bracket table is used (Table A or B).
///   6. De-annualize (÷ pay periods per year) and round to two decimal places.
///   7. Add any additional per-period withholding the employee requested on
///      Form NJ-W4.
///
/// Filing statuses (per Form NJ-W4):
///   • Status A — Single.
///     Uses Table A (single) brackets.
///   • Status B — Married/Civil Union Couple, Filing Jointly.
///     Uses Table B (married) brackets.
///   • Status C — Married/Civil Union Couple, Filing Separately.
///     Uses Table A (single) brackets.
///   • Status D — Head of Household or Qualifying Widow(er)/
///     Surviving Civil Union Partner.
///     Uses Table B (married) brackets.
///   • Status E — Surviving Civil Union Partner.
///     Uses Table B (married) brackets.
///
/// NJ has no standard deduction. Allowances reduce annual wages directly.
///
/// 2026 New Jersey amounts (NJ-WT publication):
///   Per-allowance deduction: $1,000
///   Table A — Single / Married Filing Separately (Status A and C):
///     1.40% on $0 – $20,000
///     1.75% on $20,000 – $35,000
///     3.50% on $35,000 – $40,000
///     5.53% on $40,000 – $75,000
///     6.37% on $75,000 – $500,000
///     8.97% on $500,000 – $1,000,000
///     10.75% over $1,000,000
///   Table B — Married/Civil Union, Head of Household, Surviving Partner
///     (Status B, D, and E):
///     1.40% on $0 – $20,000
///     1.75% on $20,000 – $50,000
///     2.45% on $50,000 – $70,000
///     3.50% on $70,000 – $80,000
///     5.53% on $80,000 – $150,000
///     6.37% on $150,000 – $500,000
///     8.97% on $500,000 – $1,000,000
///     10.75% over $1,000,000
///
/// Sources:
///   • New Jersey Division of Taxation, NJ-WT (Employer's Guide to
///     New Jersey Gross Income Tax Withholding), 2026.
///   • Form NJ-W4, Employee's Withholding Allowance Certificate.
/// </summary>
public sealed class NewJerseyWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Per-allowance deduction ───────────────────────────────────────

    /// <summary>
    /// Annual deduction per allowance claimed on Form NJ-W4.
    /// New Jersey uses $1,000 per allowance; this reduces annual taxable
    /// wages before the brackets are applied.
    /// </summary>
    public const decimal AllowanceAmount = 1_000m;

    // ── Table A bracket ceilings (Single / Married Filing Separately) ─

    /// <summary>Upper ceiling of the first Table A bracket ($20,000).</summary>
    public const decimal SingleBracket1Ceiling = 20_000m;

    /// <summary>Upper ceiling of the second Table A bracket ($35,000).</summary>
    public const decimal SingleBracket2Ceiling = 35_000m;

    /// <summary>Upper ceiling of the third Table A bracket ($40,000).</summary>
    public const decimal SingleBracket3Ceiling = 40_000m;

    /// <summary>Upper ceiling of the fourth Table A bracket ($75,000).</summary>
    public const decimal SingleBracket4Ceiling = 75_000m;

    /// <summary>Upper ceiling of the fifth Table A bracket ($500,000).</summary>
    public const decimal SingleBracket5Ceiling = 500_000m;

    /// <summary>Upper ceiling of the sixth Table A bracket ($1,000,000).</summary>
    public const decimal SingleBracket6Ceiling = 1_000_000m;

    // ── Table B bracket ceilings (Married / HoH / Surviving Partner) ──

    /// <summary>Upper ceiling of the first Table B bracket ($20,000).</summary>
    public const decimal MarriedBracket1Ceiling = 20_000m;

    /// <summary>Upper ceiling of the second Table B bracket ($50,000).</summary>
    public const decimal MarriedBracket2Ceiling = 50_000m;

    /// <summary>Upper ceiling of the third Table B bracket ($70,000).</summary>
    public const decimal MarriedBracket3Ceiling = 70_000m;

    /// <summary>Upper ceiling of the fourth Table B bracket ($80,000).</summary>
    public const decimal MarriedBracket4Ceiling = 80_000m;

    /// <summary>Upper ceiling of the fifth Table B bracket ($150,000).</summary>
    public const decimal MarriedBracket5Ceiling = 150_000m;

    /// <summary>Upper ceiling of the sixth Table B bracket ($500,000).</summary>
    public const decimal MarriedBracket6Ceiling = 500_000m;

    /// <summary>Upper ceiling of the seventh Table B bracket ($1,000,000).</summary>
    public const decimal MarriedBracket7Ceiling = 1_000_000m;

    // ── Tax rates ────────────────────────────────────────────────────

    /// <summary>New Jersey rate for the first bracket (1.40%).</summary>
    public const decimal Rate1 = 0.0140m;

    /// <summary>New Jersey rate for the second bracket (1.75%).</summary>
    public const decimal Rate2 = 0.0175m;

    /// <summary>New Jersey rate for Table B third bracket (2.45%).</summary>
    public const decimal RateTableB3 = 0.0245m;

    /// <summary>New Jersey rate for the 3.50% bracket.</summary>
    public const decimal Rate350 = 0.035m;

    /// <summary>New Jersey rate for the 5.53% bracket.</summary>
    public const decimal Rate553 = 0.0553m;

    /// <summary>New Jersey rate for the 6.37% bracket.</summary>
    public const decimal Rate637 = 0.0637m;

    /// <summary>New Jersey rate for the 8.97% bracket.</summary>
    public const decimal Rate897 = 0.0897m;

    /// <summary>New Jersey top rate for the 10.75% bracket.</summary>
    public const decimal Rate1075 = 0.1075m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusA = "Single (Status A)";
    public const string StatusB = "Married/Civil Union (Status B)";
    public const string StatusC = "Married/Civil Union Sep. (Status C)";
    public const string StatusD = "Head of Household (Status D)";
    public const string StatusE = "Surviving Partner (Status E)";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public NewJerseyWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.NJ, "FilingStatus");
    }

    public UsState State => UsState.NJ;

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
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusA);
        var allowances       = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract NJ-W4 allowances ($1,000 each).
        // NJ has no standard deduction; allowances reduce annual wages directly.
        var annualTaxableIncome = annualWages - allowances * AllowanceAmount;

        // Step 4: Floor annual taxable income at zero.
        annualTaxableIncome = Math.Max(0m, annualTaxableIncome);

        // Step 5: Apply NJ's 2026 graduated brackets.
        // Status A and C use Table A (single) brackets.
        // Status B, D, and E use Table B (married/HoH/surviving) brackets.
        var useMarriedTable = filingStatus == StatusB
                           || filingStatus == StatusD
                           || filingStatus == StatusE;

        var annualTax = useMarriedTable
            ? ApplyTableBBrackets(annualTaxableIncome)
            : ApplyTableABrackets(annualTaxableIncome);

        // Step 6: De-annualize and round to two decimal places.
        var withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding  = withholding
        };
    }

    // ── Bracket helpers ───────────────────────────────────────────────

    /// <summary>
    /// Applies NJ Table A brackets (Single and Married Filing Separately —
    /// NJ-W4 Status A and C).
    /// </summary>
    private static decimal ApplyTableABrackets(decimal income)
    {
        // Table A:
        //   1.40% on $0 – $20,000
        //   1.75% on $20,000 – $35,000
        //   3.50% on $35,000 – $40,000
        //   5.53% on $40,000 – $75,000
        //   6.37% on $75,000 – $500,000
        //   8.97% on $500,000 – $1,000,000
        //   10.75% over $1,000,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, SingleBracket1Ceiling) * Rate1;

        if (income > SingleBracket1Ceiling)
            tax += (Math.Min(income, SingleBracket2Ceiling) - SingleBracket1Ceiling) * Rate2;

        if (income > SingleBracket2Ceiling)
            tax += (Math.Min(income, SingleBracket3Ceiling) - SingleBracket2Ceiling) * Rate350;

        if (income > SingleBracket3Ceiling)
            tax += (Math.Min(income, SingleBracket4Ceiling) - SingleBracket3Ceiling) * Rate553;

        if (income > SingleBracket4Ceiling)
            tax += (Math.Min(income, SingleBracket5Ceiling) - SingleBracket4Ceiling) * Rate637;

        if (income > SingleBracket5Ceiling)
            tax += (Math.Min(income, SingleBracket6Ceiling) - SingleBracket5Ceiling) * Rate897;

        if (income > SingleBracket6Ceiling)
            tax += (income - SingleBracket6Ceiling) * Rate1075;

        return tax;
    }

    /// <summary>
    /// Applies NJ Table B brackets (Married/Civil Union, Head of Household,
    /// and Surviving Partner — NJ-W4 Status B, D, and E).
    /// </summary>
    private static decimal ApplyTableBBrackets(decimal income)
    {
        // Table B:
        //   1.40% on $0 – $20,000
        //   1.75% on $20,000 – $50,000
        //   2.45% on $50,000 – $70,000
        //   3.50% on $70,000 – $80,000
        //   5.53% on $80,000 – $150,000
        //   6.37% on $150,000 – $500,000
        //   8.97% on $500,000 – $1,000,000
        //   10.75% over $1,000,000
        if (income <= 0m) return 0m;

        decimal tax = 0m;

        tax += Math.Min(income, MarriedBracket1Ceiling) * Rate1;

        if (income > MarriedBracket1Ceiling)
            tax += (Math.Min(income, MarriedBracket2Ceiling) - MarriedBracket1Ceiling) * Rate2;

        if (income > MarriedBracket2Ceiling)
            tax += (Math.Min(income, MarriedBracket3Ceiling) - MarriedBracket2Ceiling) * RateTableB3;

        if (income > MarriedBracket3Ceiling)
            tax += (Math.Min(income, MarriedBracket4Ceiling) - MarriedBracket3Ceiling) * Rate350;

        if (income > MarriedBracket4Ceiling)
            tax += (Math.Min(income, MarriedBracket5Ceiling) - MarriedBracket4Ceiling) * Rate553;

        if (income > MarriedBracket5Ceiling)
            tax += (Math.Min(income, MarriedBracket6Ceiling) - MarriedBracket5Ceiling) * Rate637;

        if (income > MarriedBracket6Ceiling)
            tax += (Math.Min(income, MarriedBracket7Ceiling) - MarriedBracket6Ceiling) * Rate897;

        if (income > MarriedBracket7Ceiling)
            tax += (income - MarriedBracket7Ceiling) * Rate1075;

        return tax;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly   => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
