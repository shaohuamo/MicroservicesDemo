using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.Repositories;

internal sealed class IdempotencyRepository(ApplicationDbContext dbContext)
    : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetAsync(
        string userId,
        IdempotencyOperation operation,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            record => record.UserId == userId &&
                      record.Operation == operation &&
                      record.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public void Add(IdempotencyRecord record) =>
        dbContext.IdempotencyRecords.Add(record);

    public Task<int> DeleteExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRecords
            .Where(record => record.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
