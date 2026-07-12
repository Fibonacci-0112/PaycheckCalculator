using System.Text.Json;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.Oklahoma;

public sealed class OklahomaOw2PercentageCalculator
{
    private readonly Ow2Root _data;

    public OklahomaOw2PercentageCalculator(string json)
    {
        json = json.Replace("None", "null");
        _data = JsonSerializer.Deserialize<Ow2Root>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to load OW-2 JSON data.");
    }

    public decimal GetAllowanceAmount(PayFrequency frequency)
    {
        var key = FrequencyKey(frequency);
        if (!_data.AllowanceAmounts.TryGetValue(key, out var amt))
            throw new KeyNotFoundException($"OW-2 allowance amount not found for frequency '{key}'.");
        return amt;
    }

    public decimal CalculateWithholding(decimal okTaxableWagesAfterAllowances, PayFrequency frequency, FilingStatus status)
    {
        if (okTaxableWagesAfterAllowances <= 0m) return 0m;

        var key = FrequencyKey(frequency);
        var table = _data.Tables.FirstOrDefault(t => string.Equals(t.Frequency, key, StringComparison.OrdinalIgnoreCase))
                    ?? throw new KeyNotFoundException($"OW-2 table not found for frequency '{key}'.");

        var brackets = status == FilingStatus.Married ? table.Married : table.Single;
        var b = FindBracket(brackets, okTaxableWagesAfterAllowances);

        var tax = b.Base + (okTaxableWagesAfterAllowances - b.ExcessOver) * b.Rate;
        return RoundToNearestWholeDollar(tax);
    }

    private static Ow2Bracket FindBracket(List<Ow2Bracket> brackets, decimal wages)
    {
        foreach (var b in brackets)
        {
            var underOk = b.Under is null || wages < b.Under.Value;
            if (wages >= b.Over && underOk) return b;
        }
        return brackets.Last();
    }

    private static decimal RoundToNearestWholeDollar(decimal amount)
    {
        var dollars = Math.Floor(amount);
        var cents = amount - dollars;
        return cents < 0.50m ? dollars : dollars + 1m;
    }

    private static string FrequencyKey(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => "weekly",
        PayFrequency.Biweekly => "biweekly",
        PayFrequency.Semimonthly => "semimonthly",
        PayFrequency.Monthly => "monthly",
        PayFrequency.Quarterly => "quarterly",
        PayFrequency.Semiannual => "semiannual",
        PayFrequency.Annual => "annual",
        PayFrequency.Daily => "daily",
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
