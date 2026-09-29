namespace ProductsMicroservice.Infrastructure.Options;

public sealed class ProductOperationOutboxOptions
{
    public const string SectionName = "ProductOperationOutbox";

    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 30;
    public int MaxRetryDelaySeconds { get; set; } = 60;
}
