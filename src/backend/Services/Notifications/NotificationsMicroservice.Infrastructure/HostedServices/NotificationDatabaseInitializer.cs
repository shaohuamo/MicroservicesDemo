using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationsMicroservice.Core.Abstractions;

namespace NotificationsMicroservice.Infrastructure.HostedServices;

public sealed class NotificationDatabaseInitializer(
    INotificationDatabaseVerifier databaseVerifier,
    ILogger<NotificationDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                await databaseVerifier.VerifyConnectionAsync(cancellationToken);
                logger.LogInformation("Notification database connection is ready.");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 30) + Random.Shared.NextDouble());
                logger.LogWarning(
                    exception,
                    "Notification database initialization attempt {Attempt} failed; retrying in {DelaySeconds:n1}s.",
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
