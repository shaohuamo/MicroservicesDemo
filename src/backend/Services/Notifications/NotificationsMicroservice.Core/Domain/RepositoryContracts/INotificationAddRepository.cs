using NotificationsMicroservice.Core.Domain;

namespace NotificationsMicroservice.Core.Domain.RepositoryContracts;

public interface INotificationAddRepository
{
    Task<NotificationStoreResult> StoreAsync(
        ProductOperationNotification notification,
        string payloadHash,
        CancellationToken cancellationToken);
}
