using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace NotificationsMicroservice.API.Health;

public sealed class NotificationRedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy("Notification Redis is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Notification Redis check failed.", exception);
        }
    }
}
