using Microsoft.JSInterop;

namespace PaycheckCalculator.Blazor.Services;

/// <summary>
/// The outcome of a <see cref="BrowserLocalStorage"/> read.
/// </summary>
/// <param name="Available">
/// False when the browser could not be reached at all — during server-side prerendering (no JS
/// interop yet) or after the circuit has disconnected. Callers must treat this as "unknown", not
/// as "empty", and retry once the circuit is interactive.
/// </param>
/// <param name="Value">The stored string, or null when the key is genuinely absent.</param>
public readonly record struct StorageRead(bool Available, string? Value);

/// <summary>
/// Thin wrapper over the browser's <c>localStorage</c> (via the <c>paycheckStorage</c> helpers in
/// <c>wwwroot/export.js</c>), scoped to the Blazor circuit.
///
/// Every operation is best-effort: JS interop is unavailable during prerendering and after a
/// circuit disconnects, and <c>localStorage</c> itself throws in private-browsing modes and when
/// the origin's quota is exhausted. None of those are errors the user can act on, so reads report
/// <see cref="StorageRead.Available"/> = false and writes fail silently — the in-memory store
/// keeps working either way, which is exactly the behaviour that existed before persistence.
/// </summary>
public sealed class BrowserLocalStorage(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;

    /// <summary>Reads a key. See <see cref="StorageRead"/> for the available/absent distinction.</summary>
    public async Task<StorageRead> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _js.InvokeAsync<string?>("paycheckStorage.get", ct, key).ConfigureAwait(false);
            return new StorageRead(true, value);
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            return new StorageRead(false, null);
        }
    }

    /// <summary>Writes a key, ignoring an unreachable browser or a full quota.</summary>
    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("paycheckStorage.set", ct, key, value).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            // Best-effort: the in-memory store remains the source of truth for this circuit.
        }
    }

    /// <summary>Removes a key, ignoring an unreachable browser.</summary>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _js.InvokeVoidAsync("paycheckStorage.remove", ct, key).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            // Best-effort; see SetAsync.
        }
    }

    /// <summary>
    /// True for every way the browser can be out of reach: prerendering
    /// (<see cref="InvalidOperationException"/>), a closed circuit
    /// (<see cref="JSDisconnectedException"/>), a throwing <c>localStorage</c>
    /// (<see cref="JSException"/>), or a cancelled/torn-down call.
    /// </summary>
    private static bool IsUnreachable(Exception ex) =>
        ex is JSDisconnectedException
           or JSException
           or InvalidOperationException
           or ObjectDisposedException
           or OperationCanceledException;
}
