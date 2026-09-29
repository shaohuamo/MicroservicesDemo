using NotificationsMicroservice.Core.Domain;

namespace NotificationsMicroservice.Core.Domain.RepositoryContracts;

public interface INotificationUpdateRepository
{
    Task<bool> AcknowledgeAsync(string userId, Guid notificationId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(string userId, Guid notificationId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(string userId, long upToSequence, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> ClaimDueAsync(string workerId, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<bool> MarkAwaitingSseAckAsync(Guid notificationId, long expectedVersion, string workerId, DateTimeOffset ackDeadlineUtc, CancellationToken cancellationToken);
    Task<long?> BeginSendingEmailAsync(Guid notificationId, long expectedVersion, string workerId, CancellationToken cancellationToken);
    Task<bool> MarkDeliveredEmailAsync(Guid notificationId, long expectedVersion, string workerId, string providerMessageId, CancellationToken cancellationToken);
    Task<bool> ScheduleRetryAsync(Guid notificationId, long expectedVersion, string workerId, DateTimeOffset nextAttemptAtUtc, string errorCode, CancellationToken cancellationToken);
    Task<bool> MarkFailedAsync(Guid notificationId, long expectedVersion, string workerId, string errorCode, CancellationToken cancellationToken);
}
