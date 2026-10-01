using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ApiGateway.Revocation;

public interface IRedisRevocationGuard
{
    Task<(bool denied, bool fallback)> CheckAsync(string jti);
}

public sealed class RedisRevocationGuard(IServiceProvider services, IConfiguration configuration, TimeProvider clock)
    : IRedisRevocationGuard
{
    private readonly RedisRecoveryState state = new();
    private static long evictedKeys;
    private static readonly Meter Meter = new("ApiGateway.Redis");
    private static readonly ObservableGauge<long> evictedGauge = Meter.CreateObservableGauge(
        "gateway_redis_evicted_keys", () => Interlocked.Read(ref evictedKeys));

    // INFO detects a replaced Redis process, including when it recovered between requests.
    public async Task<(bool denied, bool fallback)> CheckAsync(string jti)
    {
        try
        {
            var redis = services.GetRequiredService<IConnectionMultiplexer>();
            if (!redis.IsConnected) throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis disconnected.");
            var server = redis.GetServer(redis.GetEndPoints().First());
            var info = (string?)await server.ExecuteAsync("INFO");
            var currentRunId = info?.Split('\n').FirstOrDefault(line => line.StartsWith("run_id:", StringComparison.Ordinal))?.Trim()[7..];
            var uptimeText = info?.Split('\n').FirstOrDefault(line => line.StartsWith("uptime_in_seconds:", StringComparison.Ordinal))?.Trim()[18..];
            var evictedText = info?.Split('\n').FirstOrDefault(line => line.StartsWith("evicted_keys:", StringComparison.Ordinal))?.Trim()[13..];
            if (long.TryParse(evictedText, out var evicted)) Interlocked.Exchange(ref evictedKeys, evicted);
            if (string.IsNullOrWhiteSpace(currentRunId) || !long.TryParse(uptimeText, out var uptime))
                throw new InvalidOperationException("Redis server identity is unavailable.");

            var now = clock.GetUtcNow();
            var fallback = state.Observe(currentRunId, uptime, now);

            var prefix = configuration["Authentication:AccessTokenDenylistPrefix"] ?? "admin-web:access-token-denylist";
            var denied = await redis.GetDatabase().KeyExistsAsync($"{prefix}:{jti}");
            return (denied, fallback);
        }
        catch
        {
            state.Failed();
            return (false, true);
        }
    }
}
