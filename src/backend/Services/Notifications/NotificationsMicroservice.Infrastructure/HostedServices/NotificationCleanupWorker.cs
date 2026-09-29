using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Options;

namespace NotificationsMicroservice.Infrastructure.HostedServices;

public sealed class NotificationCleanupWorker(
    INotificationDeleteRepository repository,
    IOptions<DeliveryOptions> options,
    ILogger<NotificationCleanupWorker> logger) : BackgroundService
{
    private readonly DeliveryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await repository.DeleteCompletedBeforeAsync(
                    DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays), stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} expired completed notifications.", deleted);
                }
                await Task.Delay(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification cleanup failed.");
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollingIntervalMilliseconds), stoppingToken);
            }
        }
    }

    private void ValidateOptions()
    {
        if (_options.PollingIntervalMilliseconds <= 0 || _options.RetentionDays <= 0 || _options.CleanupIntervalMinutes <= 0)
        {
            throw new InvalidOperationException("Notification cleanup configuration contains invalid values.");
        }
    }
}
