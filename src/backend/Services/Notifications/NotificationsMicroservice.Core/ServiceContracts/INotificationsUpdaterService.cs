namespace NotificationsMicroservice.Core.ServiceContracts;

public interface INotificationsUpdaterService
{
    Task<bool> AcknowledgeAsync(string userId, Guid notificationId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(string userId, Guid notificationId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(string userId, long upToSequence, CancellationToken cancellationToken);
}
