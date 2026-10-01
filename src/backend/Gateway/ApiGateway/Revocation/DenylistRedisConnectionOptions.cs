namespace ApiGateway.Revocation;

public sealed class DenylistRedisConnectionOptions
{
    public const string SectionName = "Authentication:DenylistRedis";

    public bool AbortOnConnectFail { get; init; }
    public int ConnectRetry { get; init; }
    public int ConnectTimeout { get; init; }
    public int SyncTimeout { get; init; }
    public int AsyncTimeout { get; init; }

    public void Validate()
    {
        if (ConnectRetry < 0 || ConnectTimeout <= 0 || SyncTimeout <= 0 || AsyncTimeout <= 0)
            throw new InvalidOperationException(
                "Gateway denylist Redis connection settings require a non-negative retry count and positive timeouts in milliseconds.");
    }
}
