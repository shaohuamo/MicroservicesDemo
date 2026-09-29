namespace NotificationsMicroservice.Core.DTO;

public sealed record NotificationHistoryPage(
    IReadOnlyList<NotificationItem> Items,
    long? NextBeforeSequence,
    long UnreadCount,
    long Watermark);
