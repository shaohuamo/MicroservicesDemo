namespace ProductsMicroservice.Infrastructure.Messaging.Outbox;

internal sealed class ProductOperationOutbox
{
    public Guid NotificationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public long Version { get; set; }
}
