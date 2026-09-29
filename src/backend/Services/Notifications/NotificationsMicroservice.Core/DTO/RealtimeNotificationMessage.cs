namespace NotificationsMicroservice.Core.DTO;

public sealed record RealtimeNotificationMessage(
    string UserId,
    Guid NotificationId,
    long SequenceNumber,
    string Operation,
    string Status,
    Guid? ProductId,
    string? ProductName,
    DateTimeOffset OccurredAtUtc,
    string? ErrorCode,
    string? TraceParent = null,
    string? TraceState = null);
