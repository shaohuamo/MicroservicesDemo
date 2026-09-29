using NotificationsMicroservice.Core.DTO;

namespace NotificationsMicroservice.Core.Abstractions;

public interface IPresencePublisher
{
    Task<IReadOnlyCollection<string>> GetActiveBffInstancesAsync(
        string userId,
        CancellationToken cancellationToken);

    Task PublishAsync(
        IReadOnlyCollection<string> bffInstanceIds,
        RealtimeNotificationMessage message,
        CancellationToken cancellationToken);
}
