using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Shared.Json;

/// <summary>
/// Serializes <see cref="StateInputValues"/> (a <c>Dictionary&lt;string, object?&gt;</c>) so that
/// values round-trip as real CLR primitives — <see cref="string"/>, <see cref="bool"/>,
/// <see cref="int"/>, <see cref="decimal"/>, or <c>null</c> — never as <see cref="JsonElement"/>.
/// <para>
/// This matters because <see cref="StateInputValues.GetValueOrDefault{T}"/> first checks
/// <c>raw is T</c>; a <see cref="JsonElement"/> would never match and would fall through to the
/// numeric <c>Convert.*</c> coercions only for numbers, breaking string/bool reads. The values the
/// UIs store are exactly these primitives, so materializing them as primitives keeps reads working.
/// </para>
/// </summary>
public sealed class StateInputValuesJsonConverter : JsonConverter<StateInputValues>
{
    public override StateInputValues Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected an object for {nameof(StateInputValues)}, got {reader.TokenType}.");

        var values = new StateInputValues();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return values;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected a property name in {nameof(StateInputValues)}.");

            var key = reader.GetString()!;
            reader.Read();
            values[key] = ReadValue(ref reader);
        }

        throw new JsonException($"Unexpected end of JSON while reading {nameof(StateInputValues)}.");
    }

    private static object? ReadValue(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Null => null,
        // Integral numbers come back as int; anything with a fractional part as decimal. A decimal
        // with scale 0 (e.g. 2m) serializes as "2" and reads back as int — harmless, because the
        // consuming calculators coerce via Convert.ToDecimal/ToInt32 in GetValueOrDefault<T>.
        JsonTokenType.Number => reader.TryGetInt32(out var i) ? i : reader.GetDecimal(),
        _ => throw new JsonException($"Unsupported token {reader.TokenType} in {nameof(StateInputValues)}.")
    };

    public override void Write(Utf8JsonWriter writer, StateInputValues value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, raw) in value)
        {
            writer.WritePropertyName(key);
            switch (raw)
            {
                case null: writer.WriteNullValue(); break;
                case bool b: writer.WriteBooleanValue(b); break;
                case string s: writer.WriteStringValue(s); break;
                case int i: writer.WriteNumberValue(i); break;
                case long l: writer.WriteNumberValue(l); break;
                case decimal d: writer.WriteNumberValue(d); break;
                case double db: writer.WriteNumberValue(db); break;
                default: writer.WriteStringValue(raw.ToString()); break;
            }
        }
        writer.WriteEndObject();
    }
}
