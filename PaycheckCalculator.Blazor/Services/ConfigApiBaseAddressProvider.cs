using PaycheckCalculator.Shared.Client;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Resolves the sync API base address from configuration (<c>PaycheckApi:BaseUrl</c>). Returns
/// <c>null</c> when unset or malformed, which disables the account/sync calls gracefully.
/// </summary>
public sealed class ConfigApiBaseAddressProvider : IApiBaseAddressProvider
{
    public ConfigApiBaseAddressProvider(IConfiguration configuration)
    {
        var url = configuration["PaycheckApi:BaseUrl"];
        BaseAddress = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }

    public Uri? BaseAddress { get; }
}
