using PaycheckCalc.Shared.Sync;

namespace PaycheckCalc.App.Services.Sync;

/// <summary>
/// Coordinates background and on-demand syncs for the MAUI app. Auto-sync requests are fire-and-forget
/// and coalesced; they no-op when the user is signed out or offline. Completion is reported on the main
/// thread via <see cref="SyncCompleted"/> so view models can refresh bound collections safely.
/// </summary>
public interface ISyncCoordinator
{
    Task<bool> IsSignedInAsync();

    /// <summary>Requests a background sync. Returns immediately; never throws.</summary>
    void RequestSync();

    /// <summary>Runs a sync now and returns its outcome (also raised on <see cref="SyncCompleted"/>).</summary>
    Task<SyncOutcome> SyncNowAsync(CancellationToken ct = default);

    event EventHandler<SyncOutcome>? SyncCompleted;
}
