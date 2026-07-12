using PaycheckCalculator.Shared.Client;
using PaycheckCalculator.Shared.Sync;

namespace PaycheckCalculator.App.Services.Sync;

/// <summary>
/// Default <see cref="ISyncCoordinator"/>. Serializes syncs behind a semaphore, coalesces redundant
/// background requests, and routes every failure into the returned outcome / the
/// <see cref="ISyncCoordinator.SyncCompleted"/> event rather than throwing into fire-and-forget code.
/// </summary>
public sealed class SyncCoordinator : ISyncCoordinator
{
    private readonly PaycheckSyncService _sync;
    private readonly ITokenStore _tokens;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _autoSyncQueued;

    public SyncCoordinator(PaycheckSyncService sync, ITokenStore tokens)
    {
        _sync = sync;
        _tokens = tokens;
    }

    public event EventHandler<SyncOutcome>? SyncCompleted;

    public async Task<bool> IsSignedInAsync()
        => await _tokens.GetTokensAsync().ConfigureAwait(false) is not null;

    public void RequestSync()
    {
        // Coalesce: while one background sync is queued/running, drop further requests.
        if (Interlocked.Exchange(ref _autoSyncQueued, 1) == 1) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await SyncNowAsync().ConfigureAwait(false);
            }
            catch
            {
                // SyncNowAsync already converts failures into outcomes; this is a final safety net.
            }
            finally
            {
                Interlocked.Exchange(ref _autoSyncQueued, 0);
            }
        });
    }

    public async Task<SyncOutcome> SyncNowAsync(CancellationToken ct = default)
    {
        if (await _tokens.GetTokensAsync(ct).ConfigureAwait(false) is null)
            return SyncOutcome.Fail("Not signed in.");
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return SyncOutcome.Fail("No internet connection.");

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        SyncOutcome outcome;
        try
        {
            outcome = await _sync.SyncAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            outcome = SyncOutcome.Fail($"Sync failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }

        RaiseCompleted(outcome);
        return outcome;
    }

    private void RaiseCompleted(SyncOutcome outcome)
    {
        var handler = SyncCompleted;
        if (handler is null) return;
        // Marshal to the UI thread so handlers can mutate bound collections.
        MainThread.BeginInvokeOnMainThread(() => handler(this, outcome));
    }
}
