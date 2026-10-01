namespace NotificationsMicroservice.Core.Domain;

public sealed class Notification
{
    public Guid NotificationId { get; init; }
    public long SequenceNumber { get; init; }
    public required string PayloadHash { get; init; }
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string Culture { get; init; }
    public required string Operation { get; init; }
    public required string Status { get; init; }
    public Guid? ProductId { get; init; }
    public string? ProductName { get; init; }
    public int? ProductVersion { get; init; }
    public string? ErrorCode { get; init; }
    public string? TraceParent { get; init; }
    public string? TraceState { get; init; }
    public string? CorrelationId { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public NotificationDeliveryStatus DeliveryStatus { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset? NextAttemptAtUtc { get; init; }
    public DateTimeOffset? AckDeadlineUtc { get; init; }
    public string? LastError { get; init; }
    public string? LockedBy { get; init; }
    public DateTimeOffset? LockedUntilUtc { get; init; }
    public long Version { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? DeliveredAtUtc { get; init; }
    public DateTimeOffset? InAppAcknowledgedAtUtc { get; init; }
    public DateTimeOffset? ReadAtUtc { get; init; }
    public string? EmailProviderMessageId { get; init; }
}
