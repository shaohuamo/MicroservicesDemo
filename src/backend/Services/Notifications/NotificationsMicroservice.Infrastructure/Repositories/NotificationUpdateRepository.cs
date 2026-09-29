using Dapper;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.Infrastructure.Repositories;

internal sealed class NotificationUpdateRepository(
    NotificationDbConnectionFactory connectionFactory,
    NotificationSqlProvider sqlProvider) : INotificationUpdateRepository
{
    /// <summary>
    /// Acknowledges an in-app notification for a user.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when the notification exists for the user, including an already acknowledged notification; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AcknowledgeAsync(string userId, Guid notificationId, CancellationToken cancellationToken) =>
        // Update_Notification_001 completes the in-app delivery and confirms the notification exists.
        UpdateAndReturnExistenceAsync(sqlProvider.Get("Update_Notification_001"), userId, notificationId, cancellationToken);

    /// <summary>
    /// Marks one notification as read and completes eligible in-app delivery states.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when the notification exists for the user, including an already read notification; otherwise, <see langword="false"/>.</returns>
    public Task<bool> MarkReadAsync(string userId, Guid notificationId, CancellationToken cancellationToken) =>
        // Update_Notification_002 marks one unread notification as read.
        UpdateAndReturnExistenceAsync(sqlProvider.Get("Update_Notification_002"), userId, notificationId, cancellationToken);

    /// <summary>
    /// Marks all unread notifications through the specified sequence as read.
    /// </summary>
    /// <returns>Returns the number of unread notification rows updated.</returns>
    public async Task<int> MarkAllReadAsync(string userId, long upToSequence, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Update_Notification_003 marks all eligible notifications up to the sequence as read.
        return await connection.ExecuteAsync(new CommandDefinition(sqlProvider.Get("Update_Notification_003"), new { UserId = userId, UpToSequence = upToSequence }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Claims due deliveries for a worker using a lease and row-level locking.
    /// </summary>
    /// <returns>Returns the deliveries successfully claimed by the worker, or an empty collection when none are due.</returns>
    public async Task<IReadOnlyList<Notification>> ClaimDueAsync(string workerId, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Update_Notification_004 claims due/retryable notifications and returns the claimed rows.
        var rows = await connection.QueryAsync<ClaimedDeliveryRow>(new CommandDefinition(sqlProvider.Get("Update_Notification_004"), new { WorkerId = workerId, BatchSize = batchSize, LeaseDuration = leaseDuration }, cancellationToken: cancellationToken));
        return rows.Select(ToNotification).ToList();
    }

    /// <summary>
    /// Moves an SSE delivery to the state in which an acknowledgement is expected.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when the delivery state was updated; otherwise, <see langword="false"/> if the version, worker, or state no longer matched.</returns>
    public Task<bool> MarkAwaitingSseAckAsync(Guid notificationId, long expectedVersion, string workerId, DateTimeOffset ackDeadlineUtc, CancellationToken cancellationToken) =>
        // Update_Notification_005 records the SSE acknowledgement deadline.
        ExecuteConditionalUpdateAsync(sqlProvider.Get("Update_Notification_005"), new { NotificationId = notificationId, ExpectedVersion = expectedVersion, WorkerId = workerId, AckDeadlineUtc = ackDeadlineUtc }, cancellationToken);

    /// <summary>
    /// Begins an email delivery and returns the incremented row version when successful.
    /// </summary>
    /// <returns>Returns the new delivery version when email sending begins; otherwise, <see langword="null"/> when the conditional update fails.</returns>
    public async Task<long?> BeginSendingEmailAsync(Guid notificationId, long expectedVersion, string workerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Update_Notification_006 atomically transitions a claimed notification to SendingEmail.
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sqlProvider.Get("Update_Notification_006"), new { NotificationId = notificationId, ExpectedVersion = expectedVersion, WorkerId = workerId }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Marks an email delivery as completed and stores the provider message ID.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when the email delivery was marked delivered; otherwise, <see langword="false"/>.</returns>
    public Task<bool> MarkDeliveredEmailAsync(Guid notificationId, long expectedVersion, string workerId, string providerMessageId, CancellationToken cancellationToken) =>
        // Update_Notification_007 completes the email delivery and releases its lease.
        ExecuteConditionalUpdateAsync(sqlProvider.Get("Update_Notification_007"), new { NotificationId = notificationId, ExpectedVersion = expectedVersion, WorkerId = workerId, ProviderMessageId = providerMessageId }, cancellationToken);

    /// <summary>
    /// Schedules the next delivery attempt and records the delivery error.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when a retry was scheduled; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ScheduleRetryAsync(Guid notificationId, long expectedVersion, string workerId, DateTimeOffset nextAttemptAtUtc, string errorCode, CancellationToken cancellationToken) =>
        // Update_Notification_008 schedules a retry and clears the current worker lease.
        ExecuteConditionalUpdateAsync(sqlProvider.Get("Update_Notification_008"), new { NotificationId = notificationId, ExpectedVersion = expectedVersion, WorkerId = workerId, NextAttemptAtUtc = nextAttemptAtUtc, ErrorCode = errorCode }, cancellationToken);

    /// <summary>
    /// Permanently marks a delivery as failed and records the final error.
    /// </summary>
    /// <returns>Returns <see langword="true"/> when the delivery was marked failed; otherwise, <see langword="false"/>.</returns>
    public Task<bool> MarkFailedAsync(Guid notificationId, long expectedVersion, string workerId, string errorCode, CancellationToken cancellationToken) =>
        // Update_Notification_009 marks the notification failed and releases its lease.
        ExecuteConditionalUpdateAsync(sqlProvider.Get("Update_Notification_009"), new { NotificationId = notificationId, ExpectedVersion = expectedVersion, WorkerId = workerId, ErrorCode = errorCode }, cancellationToken);

    private async Task<bool> UpdateAndReturnExistenceAsync(string sql, string userId, Guid notificationId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { UserId = userId, NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    private async Task<bool> ExecuteConditionalUpdateAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) == 1;
    }

    private static Notification ToNotification(ClaimedDeliveryRow row) => new()
    {
        NotificationId = row.NotificationId, SequenceNumber = row.SequenceNumber, PayloadHash = row.PayloadHash, UserId = row.UserId, UserEmail = row.UserEmail, Culture = row.Culture, Operation = row.Operation, Status = row.Status, ProductId = row.ProductId, ProductName = row.ProductName, ProductVersion = row.ProductVersion, ErrorCode = row.ErrorCode, TraceParent = row.TraceParent, TraceState = row.TraceState, CorrelationId = row.CorrelationId, OccurredAtUtc = row.OccurredAtUtc, DeliveryStatus = Enum.Parse<NotificationDeliveryStatus>(row.DeliveryStatus), AttemptCount = row.AttemptCount, NextAttemptAtUtc = row.NextAttemptAtUtc, AckDeadlineUtc = row.AckDeadlineUtc, LastError = row.LastError, LockedBy = row.LockedBy, LockedUntilUtc = row.LockedUntilUtc, Version = row.Version, CreatedAtUtc = row.CreatedAtUtc, DeliveredAtUtc = row.DeliveredAtUtc, ReadAtUtc = row.ReadAtUtc, EmailProviderMessageId = row.EmailProviderMessageId
    };

    private sealed class ClaimedDeliveryRow
    {
        public Guid NotificationId { get; init; } public long SequenceNumber { get; init; } public string PayloadHash { get; init; } = string.Empty; public string UserId { get; init; } = string.Empty; public string UserEmail { get; init; } = string.Empty; public string Culture { get; init; } = string.Empty; public string Operation { get; init; } = string.Empty; public string Status { get; init; } = string.Empty; public Guid? ProductId { get; init; } public string? ProductName { get; init; } public int? ProductVersion { get; init; } public string? ErrorCode { get; init; } public string? TraceParent { get; init; } public string? TraceState { get; init; } public string? CorrelationId { get; init; } public DateTimeOffset OccurredAtUtc { get; init; } public string DeliveryStatus { get; init; } = string.Empty; public int AttemptCount { get; init; } public DateTimeOffset? NextAttemptAtUtc { get; init; } public DateTimeOffset? AckDeadlineUtc { get; init; } public string? LastError { get; init; } public string? LockedBy { get; init; } public DateTimeOffset? LockedUntilUtc { get; init; } public long Version { get; init; } public DateTimeOffset CreatedAtUtc { get; init; } public DateTimeOffset? DeliveredAtUtc { get; init; } public DateTimeOffset? ReadAtUtc { get; init; } public string? EmailProviderMessageId { get; init; }
    }
}
