namespace NotificationsMicroservice.Core.DTO;

public sealed record NotificationReplayPage(
    IReadOnlyList<NotificationItem> Items,
    string? NextCursor,
    long Watermark);
