using PaycheckCalculator.Shared.Client;

namespace PaycheckCalculator.App.Services.Sync;

/// <summary>
/// Supplies the sync server URL from <see cref="Preferences"/>, defaulting to a local dev server. The
/// URL is user-editable on the Account page; <see cref="IApiBaseAddressProvider.BaseAddress"/> is read
/// per request so a change takes effect without rebuilding the HTTP client.
/// </summary>
public sealed class PreferencesApiBaseAddressProvider : IApiBaseAddressProvider
{
    /// <summary>Default server URL. <c>10.0.2.2</c> is the host loopback from the Android emulator.</summary>
    public const string DefaultServerUrl = "http://localhost:5201";

    private const string Key = "sync.serverUrl";

    public string ServerUrl
    {
        get => Preferences.Default.Get(Key, DefaultServerUrl);
        set => Preferences.Default.Set(Key, string.IsNullOrWhiteSpace(value) ? DefaultServerUrl : value.Trim());
    }

    public Uri? BaseAddress => Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ? uri : null;
}
