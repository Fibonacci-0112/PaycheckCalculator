using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Hawaii;

/// <summary>
/// State module for Hawaii income tax withholding.
/// Implements the annualized percentage method described in the Hawaii
/// Department of Taxation publication <em>Booklet A, Employer's Tax Guide</em>
/// ("Appendix — Percentage Method Tables for Computing Hawaii Withholding
/// Tax").
///
/// Calculation steps (Booklet A, percentage method):
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract HW-4 withholding allowances ($1,144 per allowance claimed
///      on Form HW-4).
///   5. Apply Hawaii's graduated annual brackets (1.4%–11.0%) for the
///      filing status to compute annual tax.
///   6. De-annualize (÷ pay periods per year) and round to two decimal
///      places.
///   7. Add any per-period additional withholding requested on Form HW-4.
///
/// Filing statuses (per Form HW-4):
///   • Single — used by employees who file as Single, Head of Household, or
///     Married Filing Separately; Hawaii's withholding tables for these
///     three federal statuses all use the same "Single" column.
///   • Married — Married Filing Jointly (single-earner HW-4 treatment).
///
/// 2026 Hawaii amounts (Booklet A percentage method):
///   • Standard deduction:
///       Single / HoH / MFS:        $2,200
///       Married Filing Jointly:    $4,400
///   • Annual exemption per HW-4 allowance:  $1,144
///
/// 2026 single / MFS / HoH annual brackets:
///         $0 –   $2,400    1.40%
///      $2,400 –  $4,800    3.20%
///      $4,800 –  $9,600    5.50%
///      $9,600 – $14,400    6.40%
///     $14,400 – $19,200    6.80%
///     $19,200 – $24,000    7.20%
///     $24,000 – $36,000    7.60%
///     $36,000 – $48,000    7.90%
///     $48,000 – $150,000   8.25%
///    $150,000 – $175,000   9.00%
///    $175,000 – $300,000  10.00%
///    Over $300,000        11.00%
///
/// 2026 married-filing-jointly annual brackets (single brackets ×2 through
/// the 8.25% bracket, then divergent top brackets per Booklet A):
///         $0 –   $4,800    1.40%
///      $4,800 –  $9,600    3.20%
///      $9,600 – $19,200    5.50%
///     $19,200 – $28,800    6.40%
///     $28,800 – $38,400    6.80%
///     $38,400 – $48,000    7.20%
///     $48,000 – $72,000    7.60%
///     $72,000 – $96,000    7.90%
///     $96,000 – $300,000   8.25%
///    $300,000 – $350,000   9.00%
///    $350,000 – $400,000  10.00%
///    Over $400,000        11.00%
///
/// Source: Hawaii Department of Taxation, <em>Booklet A — Employer's Tax
/// Guide</em>, Appendix: Percentage Method Tables for Computing Hawaii
/// Withholding Tax.
/// </summary>
public sealed class HawaiiWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Tax constants ────────────────────────────────────────────────

    /// <summary>2026 Hawaii standard deduction for Single / HoH / MFS.</summary>
    public const decimal StandardDeductionSingle = 2_200m;

    /// <summary>2026 Hawaii standard deduction for Married Filing Jointly.</summary>
    public const decimal StandardDeductionMarried = 4_400m;

    /// <summary>
    /// Annual exemption per HW-4 allowance. Subtracted from annual taxable
    /// wages after the standard deduction.
    /// </summary>
    public const decimal AllowanceAmount = 1_144m;

    // ── Graduated brackets ──────────────────────────────────────────

    private static readonly (decimal Floor, decimal? Ceiling, decimal Rate)[] BracketsSingle =
    [
        (       0m,   2_400m, 0.014m),
        (   2_400m,   4_800m, 0.032m),
        (   4_800m,   9_600m, 0.055m),
        (   9_600m,  14_400m, 0.064m),
        (  14_400m,  19_200m, 0.068m),
        (  19_200m,  24_000m, 0.072m),
        (  24_000m,  36_000m, 0.076m),
        (  36_000m,  48_000m, 0.079m),
        (  48_000m, 150_000m, 0.0825m),
        ( 150_000m, 175_000m, 0.09m),
        ( 175_000m, 300_000m, 0.10m),
        ( 300_000m,     null, 0.11m)
    ];

    private static readonly (decimal Floor, decimal? Ceiling, decimal Rate)[] BracketsMarried =
    [
        (       0m,   4_800m, 0.014m),
        (   4_800m,   9_600m, 0.032m),
        (   9_600m,  19_200m, 0.055m),
        (  19_200m,  28_800m, 0.064m),
        (  28_800m,  38_400m, 0.068m),
        (  38_400m,  48_000m, 0.072m),
        (  48_000m,  72_000m, 0.076m),
        (  72_000m,  96_000m, 0.079m),
        (  96_000m, 300_000m, 0.0825m),
        ( 300_000m, 350_000m, 0.09m),
        ( 350_000m, 400_000m, 0.10m),
        ( 400_000m,     null, 0.11m)
    ];

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle = "Single";
    public const string StatusMarried = "Married";

    // ── IStateWithholdingCalculator ──────────────────────────────────

    private readonly IReadOnlyList<string> _filingStatusOptions;

    public HawaiiWithholdingCalculator(IStateSchemaProvider schemaProvider)
    {
        _filingStatusOptions = schemaProvider.GetOptions(UsState.HI, "FilingStatus");
    }

    public UsState State => UsState.HI;

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
        var filingStatus = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var allowances = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract standard deduction for the HW-4 filing status.
        var standardDeduction = filingStatus == StatusMarried
            ? StandardDeductionMarried
            : StandardDeductionSingle;

        // Step 4: Subtract HW-4 allowance amounts.
        var allowanceDeduction = allowances * AllowanceAmount;

        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 5: Apply graduated brackets for the filing status.
        var brackets = filingStatus == StatusMarried ? BracketsMarried : BracketsSingle;
        var annualTax = CalculateFromBrackets(annualTaxableIncome, brackets);

        // Step 6: De-annualize and round to two decimal places.
        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 7: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static decimal CalculateFromBrackets(
        decimal income,
        (decimal Floor, decimal? Ceiling, decimal Rate)[] brackets)
    {
        decimal tax = 0m;
        foreach (var (floor, ceiling, rate) in brackets)
        {
            if (income <= floor)
                break;

            var bracketCeiling = ceiling ?? decimal.MaxValue;
            var taxableInBracket = Math.Min(income, bracketCeiling) - floor;
            tax += taxableInBracket * rate;
        }

        return tax;
    }

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
