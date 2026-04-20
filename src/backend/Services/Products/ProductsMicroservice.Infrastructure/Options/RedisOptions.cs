namespace ProductsMicroservice.Infrastructure.Options
{
    public class RedisOptions
    {
        public const string SectionName = "Redis";

        public string ConnectionString { get; set; } = "localhost:6379";
        public string InstanceName { get; set; } = "Default:";
        public int ConnectRetry { get; set; } = 3;
        public int ConnectTimeout { get; set; } = 5000;
        public int SyncTimeout { get; set; } = 5000;
        public int MaxReconnectDelay { get; set; } = 5000;
        public int InitialReconnectDelay { get; set; } = 1000;
        public bool AbortOnConnectFail { get; set; } = false;
        public int DelayedDeleteMs { get; set; } = 2000;
    }
}
