namespace ProductsMicroservice.Infrastructure.Options;

public class PostgresOptions
{
    public string Host { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 10;
}