using System.Text.Json;
using PaycheckCalc.Shared.Client;

namespace PaycheckCalc.App.Services.Sync;

/// <summary>
/// Stores auth tokens in the platform secure store (Android Keystore / Windows credential store).
/// Falls back to <see cref="Preferences"/> when <see cref="SecureStorage"/> is unavailable — notably on
/// unpackaged Windows (<c>WindowsPackageType=None</c>), where SecureStorage depends on packaged-app APIs.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "paycheckcalc.tokens";

    public async ValueTask<AuthTokens?> GetTokensAsync(CancellationToken ct = default)
    {
        var json = await GetRawAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<AuthTokens>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask SetTokensAsync(AuthTokens? tokens, CancellationToken ct = default)
    {
        if (tokens is null)
        {
            Remove();
            return;
        }
        await SetRawAsync(JsonSerializer.Serialize(tokens)).ConfigureAwait(false);
    }

    private static async Task<string?> GetRawAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false);
        }
        catch (Exception)
        {
            var pref = Preferences.Default.Get(Key, string.Empty);
            return string.IsNullOrEmpty(pref) ? null : pref;
        }
    }

    private static async Task SetRawAsync(string json)
    {
        try
        {
            await SecureStorage.Default.SetAsync(Key, json).ConfigureAwait(false);
        }
        catch (Exception)
        {
            Preferences.Default.Set(Key, json);
        }
    }

    private static void Remove()
    {
        try { SecureStorage.Default.Remove(Key); } catch { /* ignore */ }
        try { Preferences.Default.Remove(Key); } catch { /* ignore */ }
    }
}
