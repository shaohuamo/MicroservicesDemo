using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductsMicroservice.Infrastructure.Redis;

namespace ProductsMicroService.API.Health;

/// <summary>Checks whether Products Redis responds to commands.</summary>
/// <param name="connections">The shared Redis connection provider.</param>
public sealed class ProductsRedisHealthCheck(IProductsRedisConnectionProvider connections) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connections.GetConnectionAsync().WaitAsync(cancellationToken);
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy("Products Redis is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Products Redis check failed.", exception);
        }
    }
}
