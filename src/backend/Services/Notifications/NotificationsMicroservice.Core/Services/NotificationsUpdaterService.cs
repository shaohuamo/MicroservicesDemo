using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.ServiceContracts;

namespace NotificationsMicroservice.Core.Services;

public sealed class NotificationsUpdaterService(INotificationUpdateRepository repository) : INotificationsUpdaterService
{
    public Task<bool> AcknowledgeAsync(string userId, Guid notificationId, CancellationToken cancellationToken) =>
        repository.AcknowledgeAsync(userId, notificationId, cancellationToken);

    public Task<bool> MarkReadAsync(string userId, Guid notificationId, CancellationToken cancellationToken) =>
        repository.MarkReadAsync(userId, notificationId, cancellationToken);

    public Task<int> MarkAllReadAsync(string userId, long upToSequence, CancellationToken cancellationToken) =>
        repository.MarkAllReadAsync(userId, upToSequence, cancellationToken);
}
