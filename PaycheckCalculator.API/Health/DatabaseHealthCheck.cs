using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaycheckCalculator.API.Data;

namespace PaycheckCalculator.API.Health;

/// <summary>
/// Readiness probe for the sync database. Reports healthy only when the configured provider can
/// actually open a connection, so an orchestrator does not route traffic to an instance whose
/// database is unreachable. Deliberately does not run a query — readiness must stay cheap enough
/// to poll every few seconds.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SyncDbContext _db;

    public DatabaseHealthCheck(SyncDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Sync database reachable.")
                : HealthCheckResult.Unhealthy("Sync database is not reachable.");
        }
        catch (Exception ex)
        {
            // Provider exceptions can quote the connection string, so the detail is attached as the
            // structured exception rather than echoed into the probe's response body.
            return HealthCheckResult.Unhealthy("Sync database connection failed.", ex);
        }
    }
}
