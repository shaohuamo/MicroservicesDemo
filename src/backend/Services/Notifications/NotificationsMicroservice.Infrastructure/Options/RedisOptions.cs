namespace NotificationsMicroservice.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public int ConnectRetry { get; set; } = 5;
    public int ConnectTimeout { get; set; } = 5000;
    public int SyncTimeout { get; set; } = 5000;
    public bool AbortOnConnectFail { get; set; }
    public int InitialReconnectDelayMilliseconds { get; set; } = 500;
    public int MaxReconnectDelayMilliseconds { get; set; } = 5000;
}
