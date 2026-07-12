using System.Text.Json;
using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Loads state UI schemas from JSON content at construction time and caches
/// the parsed <see cref="StateFieldDefinition"/> lists for fast lookup.
/// </summary>
public sealed class JsonStateSchemaProvider : IStateSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly IReadOnlyList<StateFieldDefinition> EmptySchema = [];
    private static readonly IReadOnlyList<string> EmptyOptions = [];

    private readonly IReadOnlyDictionary<UsState, IReadOnlyList<StateFieldDefinition>> _schemas;

    /// <summary>
    /// Builds the provider from a map of state → JSON content. Each entry's
    /// value is the raw JSON string of a <c>&lt;state&gt;.json</c> file.
    /// </summary>
    public JsonStateSchemaProvider(IReadOnlyDictionary<UsState, string> stateJson)
    {
        var built = new Dictionary<UsState, IReadOnlyList<StateFieldDefinition>>();
        foreach (var (state, json) in stateJson)
            built[state] = Parse(json, state);
        _schemas = built;
    }

    public IReadOnlyList<StateFieldDefinition> GetSchema(UsState state) =>
        _schemas.TryGetValue(state, out var schema) ? schema : EmptySchema;

    public IReadOnlyList<string> GetOptions(UsState state, string fieldKey)
    {
        var schema = GetSchema(state);
        foreach (var field in schema)
        {
            if (string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase))
                return field.Options ?? EmptyOptions;
        }
        return EmptyOptions;
    }

    private static IReadOnlyList<StateFieldDefinition> Parse(string json, UsState state)
    {
        var dto = JsonSerializer.Deserialize<StateSchemaDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException(
                      $"Failed to deserialize state schema JSON for {state}.");

        var fields = new List<StateFieldDefinition>(dto.Fields.Count);
        foreach (var f in dto.Fields)
        {
            var fieldType = ParseFieldType(f.Type, state, f.Key);
            fields.Add(new StateFieldDefinition
            {
                Key = f.Key,
                Label = f.Label,
                FieldType = fieldType,
                IsRequired = f.Required,
                DefaultValue = DecodeDefault(f.Default, fieldType),
                Options = f.Options is { Count: > 0 } ? f.Options.ToArray() : null
            });
        }
        return fields;
    }

    private static StateFieldType ParseFieldType(string raw, UsState state, string key)
    {
        if (!Enum.TryParse<StateFieldType>(raw, ignoreCase: true, out var ft))
            throw new InvalidOperationException(
                $"Unknown field type '{raw}' for {state}.{key} in schema JSON.");
        return ft;
    }

    private static object? DecodeDefault(JsonElement? element, StateFieldType type)
    {
        if (element is not { } el || el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
            return null;

        return type switch
        {
            StateFieldType.Integer => el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0,
            StateFieldType.Decimal => el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : 0m,
            StateFieldType.Toggle => el.ValueKind == JsonValueKind.True
                ? true
                : el.ValueKind == JsonValueKind.False ? false : (object?)null,
            StateFieldType.Picker or StateFieldType.Text =>
                el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString(),
            _ => null
        };
    }
}
