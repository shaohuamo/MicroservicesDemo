using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using ApiGateway.Revocation;

namespace ApiGateway.Health;

public sealed class GatewayRedisHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var redis = services.GetRequiredService<IConnectionMultiplexer>();
            if (!redis.IsConnected)
                throw new InvalidOperationException("Gateway denylist Redis is disconnected.");
            await redis.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            return HealthCheckResult.Healthy("Gateway denylist Redis is reachable.");
        }
        catch (Exception exception)
        {
            try
            {
                var proof = services.GetRequiredService<SessionProofVerifier>();
                var sessions = services.GetService<IRefreshSessionStore>();
                if (proof.IsConfigured && sessions is not null && await sessions.IsAvailableAsync(cancellationToken))
                    return HealthCheckResult.Healthy("Gateway session database fallback is reachable.");
            }
            catch (Exception databaseError)
            {
                return HealthCheckResult.Unhealthy("Redis and session database are unavailable.", databaseError);
            }
            return HealthCheckResult.Unhealthy("Redis is unavailable and session fallback is disabled.", exception);
        }
    }
}
