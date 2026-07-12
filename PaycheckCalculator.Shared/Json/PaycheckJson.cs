using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalculator.Shared.Json;

/// <summary>
/// The single JSON configuration shared by every part of the sync feature — the on-device file
/// store, the HTTP client, and the API server. Using one set of options everywhere guarantees a
/// snapshot written by one component deserializes identically in another.
/// </summary>
public static class PaycheckJson
{
    /// <summary>Web defaults (camelCase, case-insensitive) plus the converters in <see cref="AddConverters"/>.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        AddConverters(options);
        return options;
    }

    /// <summary>
    /// Adds the converters the snapshot model requires. Call this on ASP.NET's
    /// <c>JsonOptions.SerializerOptions</c> so the API serializes payloads the same way the clients do.
    /// </summary>
    public static void AddConverters(JsonSerializerOptions options)
    {
        // Enums as names, never ordinals — reordering an enum must not silently corrupt stored data.
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new StateInputValuesJsonConverter());
        // Explicit ISO-8601 date serialization for BudgetTransaction.Date.
        options.Converters.Add(new DateOnlyJsonConverter());
    }
}
