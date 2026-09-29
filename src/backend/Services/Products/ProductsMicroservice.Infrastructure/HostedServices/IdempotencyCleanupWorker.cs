using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.Options;

namespace ProductsMicroservice.Infrastructure.HostedServices;

internal sealed class IdempotencyCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IdempotencyOptions> options,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(
            Math.Max(1, options.Value.CleanupIntervalMinutes));
        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();
                int deleted = await repository.DeleteExpiredAsync(
                    timeProvider.GetUtcNow(), stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} expired idempotency records", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to clean expired idempotency records");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
