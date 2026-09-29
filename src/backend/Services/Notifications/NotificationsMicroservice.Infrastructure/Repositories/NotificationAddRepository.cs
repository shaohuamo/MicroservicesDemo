using Dapper;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.Infrastructure.Repositories;

internal sealed class NotificationAddRepository(
    NotificationDbConnectionFactory connectionFactory,
    NotificationSqlProvider sqlProvider) : INotificationAddRepository
{
    /// <summary>
    /// Stores a product-operation notification for delivery, or classifies an existing notification as duplicate or payload conflict.
    /// </summary>
    /// <returns>Returns <see cref="NotificationStoreResult.Inserted"/> for a new delivery, <see cref="NotificationStoreResult.Duplicate"/> for the same payload, or <see cref="NotificationStoreResult.PayloadConflict"/> when the payload hash differs.</returns>
    public async Task<NotificationStoreResult> StoreAsync(ProductOperationNotification notification, string payloadHash, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Insert_Notification_001 inserts the notification and ignores an existing NotificationId.
        var inserted = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sqlProvider.Get("Insert_Notification_001"), new
        {
            notification.NotificationId, PayloadHash = payloadHash, notification.UserId, notification.UserEmail,
            notification.Culture, notification.Operation, notification.Status, notification.ProductId,
            notification.ProductName, notification.ProductVersion, notification.ErrorCode, notification.CorrelationId,
            notification.OccurredAtUtc, notification.TraceParent, notification.TraceState
        }, cancellationToken: cancellationToken));
        if (inserted.HasValue) return NotificationStoreResult.Inserted;

        // Select_Notification_001 reads the existing hash to distinguish duplicate payloads from conflicts.
        var storedHash = await connection.QuerySingleAsync<string>(new CommandDefinition(
            sqlProvider.Get("Select_Notification_001"),
            new { notification.NotificationId }, cancellationToken: cancellationToken));
        return string.Equals(storedHash, payloadHash, StringComparison.Ordinal)
            ? NotificationStoreResult.Duplicate : NotificationStoreResult.PayloadConflict;
    }
}
