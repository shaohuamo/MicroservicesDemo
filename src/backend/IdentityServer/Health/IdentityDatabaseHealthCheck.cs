using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityServer.Health;

public sealed class IdentityDatabaseHealthCheck(IIdentityDatabaseHealthProbe databaseProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await databaseProbe.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("IdentityServer database is reachable.")
                : HealthCheckResult.Unhealthy("IdentityServer database is unreachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("IdentityServer database check failed.", exception);
        }
    }
}
