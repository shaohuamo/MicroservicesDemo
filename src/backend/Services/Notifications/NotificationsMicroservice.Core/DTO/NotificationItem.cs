namespace NotificationsMicroservice.Core.DTO;

public sealed record NotificationItem(
    Guid NotificationId,
    long SequenceNumber,
    string Operation,
    string Status,
    Guid? ProductId,
    string? ProductName,
    int? ProductVersion,
    DateTimeOffset OccurredAtUtc,
    string? ErrorCode,
    string DeliveryStatus,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? ReadAtUtc);
