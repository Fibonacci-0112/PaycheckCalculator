namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// A flexible bag of state-specific input values keyed by the
/// <see cref="StateFieldDefinition.Key"/> strings from the calculator's schema.
/// <para>
/// Each state defines its own fields and the UI populates this dictionary
/// dynamically based on the calculator's input schema.
/// </para>
/// </summary>
public sealed class StateInputValues : Dictionary<string, object?>
{
    public StateInputValues() : base(StringComparer.OrdinalIgnoreCase) { }

    public StateInputValues(IDictionary<string, object?> source)
        : base(source, StringComparer.OrdinalIgnoreCase) { }

    /// <summary>Retrieve a typed value or <paramref name="fallback"/> when missing/null.</summary>
    public T GetValueOrDefault<T>(string key, T fallback = default!)
    {
        if (TryGetValue(key, out var raw) && raw is T typed)
            return typed;

        // Handle numeric conversions (UI may store string or different numeric type)
        if (raw != null)
        {
            try
            {
                if (typeof(T) == typeof(decimal))
                    return (T)(object)Convert.ToDecimal(raw);
                if (typeof(T) == typeof(int))
                    return (T)(object)Convert.ToInt32(raw);
                if (typeof(T) == typeof(bool))
                    return (T)(object)Convert.ToBoolean(raw);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return fallback;
            }
        }

        return fallback;
    }
}
