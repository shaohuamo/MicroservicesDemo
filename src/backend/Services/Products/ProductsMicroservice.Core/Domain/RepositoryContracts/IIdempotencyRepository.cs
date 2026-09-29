using ProductsMicroservice.Core.Domain.Entities;

namespace ProductsMicroservice.Core.Domain.RepositoryContracts;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(
        string userId,
        IdempotencyOperation operation,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    void Add(IdempotencyRecord record);

    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
