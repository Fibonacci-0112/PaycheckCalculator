using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// A tax bracket representing a marginal rate applied to income
/// between <see cref="Floor"/> and <see cref="Ceiling"/>.
/// </summary>
public sealed class TaxBracket
{
    public decimal Floor { get; init; }
    public decimal? Ceiling { get; init; }
    public decimal Rate { get; init; }
}

/// <summary>
/// Configuration for the annualized percentage-method state tax calculation.
/// Holds standard deduction, allowance amounts, and graduated brackets.
/// </summary>
public sealed class PercentageMethodConfig
{
    public decimal StandardDeductionSingle { get; init; }
    public decimal StandardDeductionMarried { get; init; }

    /// <summary>
    /// Annual dollar amount subtracted from taxable income per allowance claimed.
    /// </summary>
    public decimal AllowanceAmount { get; init; }

    /// <summary>
    /// Annual dollar credit subtracted from computed tax per allowance claimed.
    /// Used by states like Arkansas ($29), Delaware ($110), and Nebraska ($171).
    /// </summary>
    public decimal AllowanceCreditAmount { get; init; }

    public TaxBracket[] BracketsSingle { get; init; } = [];
    public TaxBracket[] BracketsMarried { get; init; } = [];
}

/// <summary>
/// Generic state tax calculator using the annualized percentage method.
/// Handles flat-rate states (single bracket) and graduated-bracket states.
/// <para>
/// Algorithm:
/// 1. Compute per-period taxable wages (gross − pre-tax deductions).
/// 2. Annualize wages (× pay periods per year).
/// 3. Subtract the standard deduction for the filing status.
/// 4. Subtract allowance deductions (allowances × per-allowance amount).
/// 5. Apply graduated brackets to compute annual tax.
/// 6. Subtract allowance credits (allowances × per-allowance credit).
/// 7. De-annualize (÷ pay periods per year) and round to two decimal places.
/// 8. Add any additional per-period withholding.
/// </para>
/// </summary>
public sealed class PercentageMethodStateTaxCalculator
{
    private readonly PercentageMethodConfig _config;

    public PercentageMethodStateTaxCalculator(UsState state, PercentageMethodConfig config)
    {
        State = state;
        _config = config;
    }

    public UsState State { get; }

    public StateTaxResult CalculateWithholding(StateTaxInput input)
    {
        var taxableWages = Math.Max(0m, input.GrossWages - input.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(input.Frequency);
        var annualWages = taxableWages * periods;

        var stdDed = input.FilingStatus == FilingStatus.Married
            ? _config.StandardDeductionMarried
            : _config.StandardDeductionSingle;
        annualWages -= stdDed;

        annualWages -= input.Allowances * _config.AllowanceAmount;
        annualWages = Math.Max(0m, annualWages);

        var brackets = input.FilingStatus == FilingStatus.Married
            ? _config.BracketsMarried
            : _config.BracketsSingle;
        var annualTax = CalculateFromBrackets(annualWages, brackets);

        annualTax -= input.Allowances * _config.AllowanceCreditAmount;
        annualTax = Math.Max(0m, annualTax);

        var periodTax = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero)
                        + input.AdditionalWithholding;

        return new StateTaxResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }

    private static decimal CalculateFromBrackets(decimal income, TaxBracket[] brackets)
    {
        decimal tax = 0m;
        foreach (var bracket in brackets)
        {
            var bracketFloor = bracket.Floor;
            if (income <= bracketFloor)
                break;

            var bracketCeiling = bracket.Ceiling ?? decimal.MaxValue;
            var taxableInBracket = Math.Min(income, bracketCeiling) - bracketFloor;
            tax += taxableInBracket * bracket.Rate;
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
