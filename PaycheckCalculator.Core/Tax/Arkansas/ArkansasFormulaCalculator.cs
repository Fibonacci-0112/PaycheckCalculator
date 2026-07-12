using System.Text.Json;

namespace PaycheckCalculator.Core.Tax.Arkansas;

/// <summary>
/// Implements the Arkansas formula method for calculating state income tax
/// withholding as published by the Arkansas Department of Finance and
/// Administration (DFA whformula_2026).
///
/// Steps:
///   1. Annualize gross pay (period gross × pay periods per year).
///   2. Subtract the standard deduction ($2,470) to get net taxable income.
///      If net taxable income is below $100,001, round to the nearest $50.
///      For $100,001 and over, use the exact dollar amount.
///   3. Compute annual gross tax using the graduated bracket table.
///      Tax = rate × net taxable income − subtraction amount.
///      Round the annual gross tax to two decimal places.
///   4. Compute personal tax credits (exemptions × $29).
///   5. Subtract personal tax credits from annual gross tax to get annual net tax.
///   6. Divide annual net tax by number of pay periods to get per-period withholding.
/// </summary>
public sealed class ArkansasFormulaCalculator
{
    private readonly ArTaxData _data;

    public ArkansasFormulaCalculator(string json)
    {
        _data = JsonSerializer.Deserialize<ArTaxData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to deserialize Arkansas withholding JSON data.");
    }

    /// <summary>
    /// Calculates the per-period Arkansas state income tax withholding.
    /// </summary>
    /// <param name="grossWagesPerPeriod">Gross wages for the current pay period.</param>
    /// <param name="payPeriodsPerYear">Number of pay periods per year.</param>
    /// <param name="exemptions">Total number of exemptions claimed on AR4EC.</param>
    /// <returns>State income tax withholding for one pay period (always ≥ 0).</returns>
    public decimal CalculateWithholding(
        decimal grossWagesPerPeriod,
        int payPeriodsPerYear,
        int exemptions)
    {
        if (grossWagesPerPeriod <= 0m || payPeriodsPerYear <= 0)
            return 0m;

        // Step 1 – Annualize gross pay
        decimal annualGrossPay = grossWagesPerPeriod * payPeriodsPerYear;

        // Step 2 – Subtract standard deduction; round if below threshold
        decimal netTaxableIncome = annualGrossPay - _data.StandardDeduction;
        if (netTaxableIncome <= 0m)
            return 0m;

        if (netTaxableIncome < _data.RoundToNearest50Threshold)
            netTaxableIncome = RoundToNearest50(netTaxableIncome);

        // Step 3 – Compute annual gross tax from the bracket table, then round
        decimal annualGrossTax = ComputeGrossTax(netTaxableIncome);
        annualGrossTax = Math.Round(annualGrossTax, 0, MidpointRounding.AwayFromZero);

        // Step 4 – Personal tax credits
        decimal personalTaxCredits = exemptions * _data.PersonalTaxCreditPerExemption;

        // Step 5 – Annual net tax
        decimal annualNetTax = Math.Max(0m, annualGrossTax - personalTaxCredits);

        // Step 6 – Per-period withholding
        decimal withholding = annualNetTax / payPeriodsPerYear;

        return Math.Round(withholding, 2);
    }

    /// <summary>
    /// Applies the formula: tax = rate × netTaxableIncome − subtraction.
    /// </summary>
    private decimal ComputeGrossTax(decimal netTaxableIncome)
    {
        var bracket = FindBracket(netTaxableIncome);
        decimal tax = bracket.Rate * netTaxableIncome - bracket.Subtraction;
        return Math.Max(0m, tax);
    }

    private ArBracket FindBracket(decimal income)
    {
        foreach (var b in _data.Brackets)
        {
            if (income >= b.From && (b.To is null || income <= b.To.Value))
                return b;
        }

        // Fallback to the last bracket (open-ended)
        return _data.Brackets[^1];
    }

    /// <summary>
    /// Rounds the amount to the nearest $50.
    /// Example: 23,054 → 23,050; 23,099 → 23,100.
    /// </summary>
    public static decimal RoundToNearest50(decimal amount)
    {
        return Math.Round(amount / 50m, 0, MidpointRounding.AwayFromZero) * 50m;
    }

    // ── JSON deserialization models ─────────────────────────────────────

    private sealed class ArTaxData
    {
        public int SchemaVersion { get; set; }
        public decimal StandardDeduction { get; set; }
        public decimal PersonalTaxCreditPerExemption { get; set; }
        public decimal RoundToNearest50Threshold { get; set; }
        public List<ArBracket> Brackets { get; set; } = [];
    }

    private sealed class ArBracket
    {
        public decimal From { get; set; }
        public decimal? To { get; set; }
        public decimal Rate { get; set; }
        public decimal Subtraction { get; set; }
    }
}
