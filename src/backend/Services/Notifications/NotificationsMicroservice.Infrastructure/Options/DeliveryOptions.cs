namespace NotificationsMicroservice.Infrastructure.Options;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    public int PollingIntervalMilliseconds { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 30;
    public int SseAckDeadlineSeconds { get; set; } = 5;
    public int InAppGraceSeconds { get; set; } = 20;
    public int MaxSseAttempts { get; set; } = 2;
    public int MaxAttempts { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 2;
    public int MaxRetryDelaySeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 60;
}
