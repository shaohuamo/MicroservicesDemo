namespace NotificationsMicroservice.Core.DTO;

public sealed record NotificationReplayPage(
    IReadOnlyList<NotificationItem> Items,
    long? NextAfterSequence,
    long Watermark);
