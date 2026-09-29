using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ProductsMicroService.API.Health;

/// <summary>Checks whether Products Redis responds to commands.</summary>
/// <param name="connection">The shared Redis connection.</param>
public sealed class ProductsRedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy("Products Redis is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Products Redis check failed.", exception);
        }
    }
}
