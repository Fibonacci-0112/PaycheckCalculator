using System.Text.Json.Serialization;

namespace PaycheckCalculator.Core.Tax.Oklahoma;

internal sealed class Ow2Root
{
    [JsonPropertyName("allowanceAmounts")]
    public Dictionary<string, decimal> AllowanceAmounts { get; set; } = new();

    [JsonPropertyName("tables")]
    public List<Ow2Table> Tables { get; set; } = new();
}

internal sealed class Ow2Table
{
    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = "";

    [JsonPropertyName("single")]
    public List<Ow2Bracket> Single { get; set; } = new();

    [JsonPropertyName("married")]
    public List<Ow2Bracket> Married { get; set; } = new();
}

internal sealed class Ow2Bracket
{
    [JsonPropertyName("over")]
    public decimal Over { get; set; }

    [JsonPropertyName("under")]
    public decimal? Under { get; set; }

    [JsonPropertyName("base")]
    public decimal Base { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("excessOver")]
    public decimal ExcessOver { get; set; }
}
