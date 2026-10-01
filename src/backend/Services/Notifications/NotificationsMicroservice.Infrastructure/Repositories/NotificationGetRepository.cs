using Dapper;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.Infrastructure.Repositories;

internal sealed class NotificationGetRepository(
    NotificationDbConnectionFactory connectionFactory,
    NotificationSqlProvider sqlProvider) : INotificationGetRepository
{
    /// <summary>
    /// Returns a user's retained notification history page, unread count, and pagination watermark.
    /// </summary>
    /// <returns>Returns a history page containing notification items, the unread count, the current watermark, and the next-page sequence when more items exist.</returns>
    public async Task<NotificationHistoryPage> GetHistoryAsync(string userId, long? beforeSequence, int limit, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Select_Notification_002 returns watermark, unread count, and history rows as three ordered result sets.
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sqlProvider.Get("Select_Notification_002"), new { UserId = userId, BeforeSequence = beforeSequence, CutoffUtc = cutoffUtc, Take = limit + 1 }, cancellationToken: cancellationToken));
        var watermark = await grid.ReadSingleAsync<long>();
        var unreadCount = await grid.ReadSingleAsync<long>();
        var rows = (await grid.ReadAsync<NotificationItemRow>()).ToList();
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = rows.Select(ToItem).ToList();
        return new NotificationHistoryPage(items, hasMore && items.Count > 0 ? items[^1].SequenceNumber : null, unreadCount, watermark);
    }

    /// <summary>Returns unacknowledged in-app notifications, newest operation first.</summary>
    public async Task<NotificationReplayPage> GetReplayAsync(string userId, NotificationReplayCursor? cursor, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var watermark = cursor?.Watermark ?? await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            sqlProvider.Get("Select_Notification_003"), new { UserId = userId }, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<NotificationItemRow>(new CommandDefinition(
            sqlProvider.Get("Select_Notification_004"),
            new { UserId = userId, Watermark = watermark, BeforeOccurredAtUtc = cursor?.BeforeOccurredAtUtc, BeforeSequence = cursor?.BeforeSequence, Take = limit + 1 },
            cancellationToken: cancellationToken))).ToList();
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = rows.Select(ToItem).ToList();
        var nextCursor = hasMore && items.Count > 0
            ? new NotificationReplayCursor(watermark, items[^1].OccurredAtUtc, items[^1].SequenceNumber).Encode()
            : null;
        return new NotificationReplayPage(items, nextCursor, watermark);
    }

    private static NotificationItem ToItem(NotificationItemRow row) => new(row.NotificationId, row.SequenceNumber, row.Operation, row.Status, row.ProductId, row.ProductName, row.ProductVersion, row.OccurredAtUtc, row.ErrorCode, row.DeliveryStatus, row.DeliveredAtUtc, row.ReadAtUtc);

    private sealed class NotificationItemRow
    {
        public Guid NotificationId { get; init; }
        public long SequenceNumber { get; init; }
        public string Operation { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public Guid? ProductId { get; init; }
        public string? ProductName { get; init; }
        public int? ProductVersion { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
        public string? ErrorCode { get; init; }
        public string DeliveryStatus { get; init; } = string.Empty;
        public DateTimeOffset? DeliveredAtUtc { get; init; }
        public DateTimeOffset? ReadAtUtc { get; init; }
    }
}
