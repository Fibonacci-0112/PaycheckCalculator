namespace PaycheckCalculator.Shared.Client;

/// <summary>
/// Supplies the API base address per request, so a client (notably MAUI, where the user can edit the
/// server URL) can change it without rebuilding <see cref="System.Net.Http.HttpClient"/> instances.
/// Returns <c>null</c> when no server is configured.
/// </summary>
public interface IApiBaseAddressProvider
{
    Uri? BaseAddress { get; }
}
