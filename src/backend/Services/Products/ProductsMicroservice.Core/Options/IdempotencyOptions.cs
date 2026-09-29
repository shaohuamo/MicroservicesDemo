namespace ProductsMicroservice.Core.Options;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";
    public int RetentionHours { get; init; } = 24;
    public int CleanupIntervalMinutes { get; init; } = 60;
}
