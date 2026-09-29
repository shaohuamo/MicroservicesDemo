using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.Messaging.Outbox;

internal sealed class ProductOperationOutboxStore : IProductOperationOutboxStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProductOperationOutboxStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<ProductOperationOutbox>> ClaimBatchAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime lockedUntil = now.Add(leaseDuration);
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<ProductOperationOutbox> candidates = await dbContext.ProductOperationOutbox
            .AsNoTracking()
            .Where(outbox => outbox.PublishedAtUtc == null
                && (outbox.NextAttemptAtUtc == null || outbox.NextAttemptAtUtc <= now)
                && (outbox.LockedUntilUtc == null || outbox.LockedUntilUtc <= now))
            .OrderBy(outbox => outbox.CreatedAtUtc)
            .ThenBy(outbox => outbox.NotificationId)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var claimed = new List<ProductOperationOutbox>(candidates.Count);
        foreach (ProductOperationOutbox candidate in candidates)
        {
            int affected = await dbContext.ProductOperationOutbox
                .Where(outbox => outbox.NotificationId == candidate.NotificationId
                    && outbox.Version == candidate.Version
                    && outbox.PublishedAtUtc == null
                    && (outbox.NextAttemptAtUtc == null || outbox.NextAttemptAtUtc <= now)
                    && (outbox.LockedUntilUtc == null || outbox.LockedUntilUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(outbox => outbox.LockedBy, workerId)
                    .SetProperty(outbox => outbox.LockedUntilUtc, lockedUntil)
                    .SetProperty(outbox => outbox.Version, outbox => outbox.Version + 1),
                    cancellationToken);

            if (affected == 1)
            {
                candidate.LockedBy = workerId;
                candidate.LockedUntilUtc = lockedUntil;
                candidate.Version++;
                claimed.Add(candidate);
            }
        }

        return claimed;
    }

    public async Task<bool> MarkPublishedAsync(
        ProductOperationOutbox outbox,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int affected = await dbContext.ProductOperationOutbox
            .Where(candidate => candidate.NotificationId == outbox.NotificationId
                && candidate.Version == outbox.Version
                && candidate.LockedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.PublishedAtUtc, now)
                .SetProperty(candidate => candidate.LockedBy, (string?)null)
                .SetProperty(candidate => candidate.LockedUntilUtc, (DateTime?)null)
                .SetProperty(candidate => candidate.LastError, (string?)null)
                .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                cancellationToken);
        return affected == 1;
    }

    public async Task ScheduleRetryAsync(
        ProductOperationOutbox outbox,
        string workerId,
        int attempt,
        TimeSpan retryDelay,
        string error,
        CancellationToken cancellationToken = default)
    {
        DateTime nextAttemptAt = DateTime.UtcNow.Add(retryDelay);
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.ProductOperationOutbox
            .Where(candidate => candidate.NotificationId == outbox.NotificationId
                && candidate.Version == outbox.Version
                && candidate.LockedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.AttemptCount, attempt)
                .SetProperty(candidate => candidate.NextAttemptAtUtc, nextAttemptAt)
                .SetProperty(candidate => candidate.LastError, error)
                .SetProperty(candidate => candidate.LockedBy, (string?)null)
                .SetProperty(candidate => candidate.LockedUntilUtc, (DateTime?)null)
                .SetProperty(candidate => candidate.Version, candidate => candidate.Version + 1),
                cancellationToken);
    }
}
