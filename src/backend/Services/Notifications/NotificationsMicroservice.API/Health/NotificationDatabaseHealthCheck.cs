using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.API.Health;

public sealed class NotificationDatabaseHealthCheck(INotificationDatabaseHealthProbe databaseProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await databaseProbe.CheckAsync(cancellationToken);
            return HealthCheckResult.Healthy("Notification database is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Notification database check failed.", exception);
        }
    }
}
