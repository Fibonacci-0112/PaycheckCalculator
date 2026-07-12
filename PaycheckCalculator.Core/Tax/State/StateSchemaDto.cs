using System.Text.Json.Serialization;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Deserialization DTO for a state schema JSON file under
/// <c>PaycheckCalculator.Core/Data/Schemas/&lt;state&gt;.json</c>.
/// </summary>
internal sealed class StateSchemaDto
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<StateFieldDto> Fields { get; set; } = [];
}

internal sealed class StateFieldDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("default")]
    public System.Text.Json.JsonElement? Default { get; set; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }
}
