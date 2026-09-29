using NotificationsMicroservice.Core.DTO;

namespace NotificationsMicroservice.Core.Domain.RepositoryContracts;

public interface INotificationGetRepository
{
    Task<NotificationHistoryPage> GetHistoryAsync(
        string userId,
        long? beforeSequence,
        int limit,
        int retentionDays,
        CancellationToken cancellationToken);

    Task<NotificationReplayPage> GetReplayAsync(
        string userId,
        long afterSequence,
        long? upToSequence,
        int limit,
        CancellationToken cancellationToken);
}
