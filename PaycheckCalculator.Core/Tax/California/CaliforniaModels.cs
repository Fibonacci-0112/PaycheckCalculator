using System.Text.Json.Serialization;

namespace PaycheckCalculator.Core.Tax.California;

internal sealed class CaliforniaMethodBData
{
    [JsonPropertyName("lowIncomeExemptionThresholds")]
    public Dictionary<string, Dictionary<string, decimal>> LowIncomeExemptionThresholds { get; set; } = new();

    [JsonPropertyName("estimatedDeductionAllowances")]
    public AllowanceLookup EstimatedDeductionAllowances { get; set; } = new();

    [JsonPropertyName("standardDeductions")]
    public Dictionary<string, Dictionary<string, decimal>> StandardDeductions { get; set; } = new();

    [JsonPropertyName("exemptionAllowanceCredits")]
    public AllowanceLookup ExemptionAllowanceCredits { get; set; } = new();

    [JsonPropertyName("taxRateTables")]
    public Dictionary<string, Dictionary<string, List<CaliforniaBracket>>> TaxRateTables { get; set; } = new();
}

internal sealed class AllowanceLookup
{
    [JsonPropertyName("maxExplicitAllowanceCount")]
    public int MaxExplicitAllowanceCount { get; set; }

    [JsonPropertyName("multiplyOneAllowanceAmountWhenGreaterThanMax")]
    public bool MultiplyOneAllowanceAmountWhenGreaterThanMax { get; set; }

    [JsonPropertyName("oneAllowanceAmountByPayrollPeriod")]
    public Dictionary<string, decimal>? OneAllowanceAmountByPayrollPeriod { get; set; }

    [JsonPropertyName("oneAllowanceCreditByPayrollPeriod")]
    public Dictionary<string, decimal>? OneAllowanceCreditByPayrollPeriod { get; set; }

    [JsonIgnore]
    private Dictionary<string, decimal> OneAllowanceByPeriod =>
        OneAllowanceAmountByPayrollPeriod ?? OneAllowanceCreditByPayrollPeriod ?? new();

    [JsonPropertyName("amountsByPayrollPeriod")]
    public Dictionary<string, List<decimal>> AmountsByPayrollPeriod { get; set; } = new();

    public decimal GetAmount(string periodKey, int allowanceCount)
    {
        if (allowanceCount <= 0) return 0m;

        var amounts = AmountsByPayrollPeriod[periodKey];
        if (allowanceCount < amounts.Count)
            return amounts[allowanceCount];

        if (MultiplyOneAllowanceAmountWhenGreaterThanMax)
            return allowanceCount * OneAllowanceByPeriod[periodKey];

        return amounts[^1];
    }
}

internal sealed class CaliforniaBracket
{
    [JsonPropertyName("over")]
    public decimal Over { get; set; }

    [JsonPropertyName("notOver")]
    public decimal? NotOver { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("amountOver")]
    public decimal AmountOver { get; set; }

    [JsonPropertyName("plus")]
    public decimal Plus { get; set; }
}
