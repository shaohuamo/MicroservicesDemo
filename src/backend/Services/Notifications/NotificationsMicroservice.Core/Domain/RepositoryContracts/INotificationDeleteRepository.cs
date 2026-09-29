namespace NotificationsMicroservice.Core.Domain.RepositoryContracts;

public interface INotificationDeleteRepository
{
    Task<int> DeleteCompletedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}
