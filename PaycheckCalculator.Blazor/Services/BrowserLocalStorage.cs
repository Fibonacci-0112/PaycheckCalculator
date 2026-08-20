using Microsoft.JSInterop;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// Thin wrapper over the browser's <c>localStorage</c> (see <c>wwwroot/localStore.js</c>) used to
/// persist anonymous, signed-out session data across browser tabs and restarts.
/// <para>
/// It deliberately deals only in opaque strings: callers serialize with
/// <see cref="Shared.Json.PaycheckJson.Options"/> so the shared converters stay authoritative.
/// </para>
/// <para>
/// Every operation is best-effort. JS interop is unavailable during prerendering, and
/// <c>localStorage</c> can throw outright in private-browsing modes, so failures degrade to
/// in-memory-only behavior instead of surfacing an error.
/// </para>
/// </summary>
public sealed class BrowserLocalStorage(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;

    /// <summary>
    /// True once a call has succeeded — i.e. the circuit is interactive and storage is usable.
    /// Callers use this to avoid writing before the first successful hydration.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Reads a value, or null when it is absent or storage is unavailable.</summary>
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _js.InvokeAsync<string?>("paycheckLocalStore.get", ct, key).ConfigureAwait(false);
            IsAvailable = true;
            return value;
        }
        catch (Exception ex) when (IsInteropUnavailable(ex))
        {
            return null;
        }
    }

    /// <summary>Writes a value. Silently does nothing when storage is unavailable or full.</summary>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("paycheckLocalStore.set", ct, key, value).ConfigureAwait(false);
            IsAvailable = true;
        }
        catch (Exception ex) when (IsInteropUnavailable(ex))
        {
            // Ignored: persistence is an enhancement, never a requirement.
        }
    }

    /// <summary>Removes a key. Silently does nothing when storage is unavailable.</summary>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("paycheckLocalStore.remove", ct, key).ConfigureAwait(false);
            IsAvailable = true;
        }
        catch (Exception ex) when (IsInteropUnavailable(ex))
        {
            // Ignored: persistence is an enhancement, never a requirement.
        }
    }

    /// <summary>
    /// True for the failure modes that mean "the browser isn't reachable right now": prerendering
    /// and disposed circuits raise <see cref="InvalidOperationException"/> /
    /// <see cref="ObjectDisposedException"/>, a disconnected circuit raises
    /// <see cref="JSDisconnectedException"/>, and script errors raise <see cref="JSException"/>.
    /// </summary>
    private static bool IsInteropUnavailable(Exception ex) =>
        ex is JSDisconnectedException
           or JSException
           or ObjectDisposedException
           or InvalidOperationException
           or OperationCanceledException
           or TaskCanceledException;
}
