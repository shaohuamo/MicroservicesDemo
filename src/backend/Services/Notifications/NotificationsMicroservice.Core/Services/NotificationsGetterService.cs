using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Core.ServiceContracts;

namespace NotificationsMicroservice.Core.Services;

public sealed class NotificationsGetterService(INotificationGetRepository repository) : INotificationsGetterService
{
    public Task<NotificationHistoryPage> GetHistoryAsync(string userId, long? beforeSequence, int limit, int retentionDays, CancellationToken cancellationToken) =>
        repository.GetHistoryAsync(userId, beforeSequence, limit, retentionDays, cancellationToken);

    public Task<NotificationReplayPage> GetReplayAsync(string userId, long afterSequence, long? upToSequence, int limit, CancellationToken cancellationToken) =>
        repository.GetReplayAsync(userId, afterSequence, upToSequence, limit, cancellationToken);
}
