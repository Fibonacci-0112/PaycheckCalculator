namespace PaycheckCalculator.Shared.Client;

/// <summary>Outcome of an API call with no payload.</summary>
public sealed record ApiResult(bool Success, string? Error)
{
    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(string error) => new(false, error);
}

/// <summary>Outcome of an API call returning a <typeparamref name="T"/> on success.</summary>
public sealed record ApiResult<T>(bool Success, T? Value, string? Error)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(string error) => new(false, default, error);
}
