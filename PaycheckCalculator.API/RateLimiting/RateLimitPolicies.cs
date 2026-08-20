using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace PaycheckCalculator.API.RateLimiting;

/// <summary>
/// Fixed-window rate limiting for the two abuse-prone surfaces: unauthenticated account endpoints
/// (register / login / refresh, which are password-guessing and account-enumeration targets) and the
/// authenticated sync endpoints (which accept whole snapshots and are the most expensive handlers).
/// <para>
/// Limits are configuration-driven so a deployment can tighten or relax them without a code change.
/// The defaults are deliberately generous rather than security-tight, because a front-end may call
/// this API <b>server-side</b> — the Blazor app proxies every user through a single outbound
/// address, so all of its users share one IP partition. Tightening the account window below the
/// default only makes sense when clients reach the API directly, or once a trusted-proxy
/// <c>X-Forwarded-For</c> configuration lets the limiter see real client addresses.
/// </para>
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Applied to <c>/api/account</c>; partitioned by client IP.</summary>
    public const string Account = "account";

    /// <summary>Applied to the sync endpoint groups; partitioned by authenticated user.</summary>
    public const string Sync = "sync";

    private const int DefaultAccountPermitPerWindow = 100;
    private const int DefaultSyncPermitPerWindow = 300;
    private const int DefaultWindowSeconds = 60;

    public static IServiceCollection AddPaycheckRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var window = TimeSpan.FromSeconds(
            Math.Max(1, section.GetValue("WindowSeconds", DefaultWindowSeconds)));
        var accountPermit = Math.Max(1, section.GetValue("AccountPermitPerWindow", DefaultAccountPermitPerWindow));
        var syncPermit = Math.Max(1, section.GetValue("SyncPermitPerWindow", DefaultSyncPermitPerWindow));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Account traffic is unauthenticated by definition, so the only partition available is
            // the caller's address. A missing address (in-process test hosts, unix sockets) collapses
            // into one shared bucket, which is the safe direction: it limits more, never less.
            options.AddPolicy(Account, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = accountPermit, Window = window }));

            // Sync callers are authenticated, so partition by user id: one noisy account cannot
            // exhaust another's budget, and a shared outbound IP is no longer a false partition.
            options.AddPolicy(Sync, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = syncPermit, Window = window }));

            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
        });
    }
}
