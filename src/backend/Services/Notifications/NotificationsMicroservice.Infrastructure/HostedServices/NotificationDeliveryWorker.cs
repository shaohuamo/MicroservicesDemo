using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Infrastructure.Options;
using NotificationsMicroservice.Infrastructure.Diagnostics;

namespace NotificationsMicroservice.Infrastructure.HostedServices;

public sealed class NotificationDeliveryWorker(
    INotificationUpdateRepository repository,
    IPresencePublisher presencePublisher,
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryOptions> options,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private readonly DeliveryOptions _options = options.Value;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}:delivery-1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification delivery scan failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(_options.PollingIntervalMilliseconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var notifications = await repository.ClaimDueAsync(
            _workerId,
            _options.BatchSize,
            TimeSpan.FromSeconds(_options.LeaseSeconds),
            cancellationToken);

        foreach (var notification in notifications)
        {
            try
            {
                await ProcessAsync(notification, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    private async Task ProcessAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        var parentContext = ActivityContext.TryParse(
            notification.TraceParent,
            notification.TraceState,
            isRemote: true,
            out var parsedContext)
            ? parsedContext
            : default;
        using Activity? deliveryActivity = NotificationsTelemetry.ActivitySource.StartActivity(
            "notifications.delivery",
            ActivityKind.Internal,
            parentContext);
        deliveryActivity?.SetTag("notification.id", notification.NotificationId);

        try
        {
            await ProcessNotificationAsync(notification, deliveryActivity, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A database transition may itself have failed. The row lease makes the
            // delivery recoverable by this or another replica after it expires.
            logger.LogError(
                exception,
                "Unexpected delivery failure for notification {NotificationId}.",
                notification.NotificationId);
            deliveryActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            deliveryActivity?.AddException(exception);
        }
    }

    private async Task ProcessNotificationAsync(
        Notification notification,
        Activity? deliveryActivity,
        CancellationToken cancellationToken)
    {
        if (notification.DeliveryStatus == NotificationDeliveryStatus.SendingEmail)
        {
            await SendEmailAsync(notification, notification.Version, cancellationToken);
            return;
        }

        var redisFailed = false;
        if (notification.AttemptCount <= _options.MaxSseAttempts)
        {
            try
            {
                var bffInstances = await presencePublisher.GetActiveBffInstancesAsync(
                    notification.UserId,
                    cancellationToken);
                if (bffInstances.Count > 0)
                {
                    using Activity? redisActivity = NotificationsTelemetry.ActivitySource.StartActivity(
                        "notifications.redis.publish",
                        ActivityKind.Producer);
                    redisActivity?.SetTag("messaging.system", "redis");
                    redisActivity?.SetTag("notification.id", notification.NotificationId);

                    var realtimeMessage = new RealtimeNotificationMessage(
                        notification.UserId,
                        notification.NotificationId,
                        notification.SequenceNumber,
                        notification.Operation,
                        notification.Status,
                        notification.ProductId,
                        notification.ProductName,
                        notification.OccurredAtUtc,
                        notification.ErrorCode,
                        redisActivity?.Id ?? deliveryActivity?.Id ?? notification.TraceParent,
                        redisActivity?.TraceStateString ?? deliveryActivity?.TraceStateString ?? notification.TraceState);

                    await presencePublisher.PublishAsync(bffInstances, realtimeMessage, cancellationToken);
                    redisActivity?.SetStatus(ActivityStatusCode.Ok);
                    var ackDeadline = DateTimeOffset.UtcNow.AddSeconds(_options.SseAckDeadlineSeconds);
                    await repository.MarkAwaitingSseAckAsync(
                        notification.NotificationId,
                        notification.Version,
                        _workerId,
                        ackDeadline,
                        cancellationToken);
                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Redis delivery failed for notification {NotificationId}.",
                    notification.NotificationId);
                redisFailed = true;
            }
        }

        // An active BFF can still deliver from PostgreSQL while Redis is down.
        // The version check on the later email transition lets a browser ACK win.
        var graceDeadline = notification.CreatedAtUtc.AddSeconds(_options.InAppGraceSeconds);
        if (DateTimeOffset.UtcNow < graceDeadline)
        {
            await repository.ScheduleRetryAsync(
                notification.NotificationId,
                notification.Version,
                _workerId,
                graceDeadline,
                redisFailed ? "REDIS_DELIVERY_FAILED" : "AWAITING_IN_APP_DELIVERY",
                cancellationToken);
            return;
        }

        var sendingVersion = await repository.BeginSendingEmailAsync(
            notification.NotificationId,
            notification.Version,
            _workerId,
            cancellationToken);
        if (!sendingVersion.HasValue)
        {
            return;
        }

        await SendEmailAsync(notification, sendingVersion.Value, cancellationToken);
    }

    private async Task SendEmailAsync(
        Notification notification,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            INotificationEmailSender emailSender =
                scope.ServiceProvider.GetRequiredService<INotificationEmailSender>();

            var providerMessageId = await emailSender.SendAsync(notification, cancellationToken);
            var updated = await repository.MarkDeliveredEmailAsync(
                notification.NotificationId,
                expectedVersion,
                _workerId,
                providerMessageId,
                cancellationToken);

            if (!updated)
            {
                logger.LogInformation(
                    "Email completion for notification {NotificationId} lost a state race; no terminal state was overwritten.",
                    notification.NotificationId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Email delivery failed for notification {NotificationId}.",
                notification.NotificationId);
            await HandleFailureAsync(notification, expectedVersion, "EMAIL_DELIVERY_FAILED", cancellationToken);
        }
    }

    private async Task HandleFailureAsync(
        Notification notification,
        long expectedVersion,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (notification.AttemptCount >= _options.MaxAttempts)
        {
            var markedFailed = await repository.MarkFailedAsync(
                notification.NotificationId,
                expectedVersion,
                _workerId,
                errorCode,
                cancellationToken);
            if (markedFailed)
            {
                logger.LogError(
                    "Notification {NotificationId} exhausted {AttemptCount} delivery attempts.",
                    notification.NotificationId,
                    notification.AttemptCount);
            }

            return;
        }

        var exponent = Math.Max(notification.AttemptCount - 1, 0);
        var retrySeconds = Math.Min(
            _options.InitialRetryDelaySeconds * Math.Pow(2, exponent),
            _options.MaxRetryDelaySeconds);
        var nextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(retrySeconds + Random.Shared.NextDouble());
        await repository.ScheduleRetryAsync(
            notification.NotificationId,
            expectedVersion,
            _workerId,
            nextAttemptUtc,
            errorCode,
            cancellationToken);
    }

    private void ValidateOptions()
    {
        if (_options.PollingIntervalMilliseconds <= 0
            || _options.BatchSize <= 0
            || _options.LeaseSeconds <= 0
            || _options.SseAckDeadlineSeconds <= 0
            || _options.InAppGraceSeconds <= 0
            || _options.MaxSseAttempts <= 0
            || _options.MaxAttempts < _options.MaxSseAttempts
            || _options.InitialRetryDelaySeconds <= 0
            || _options.MaxRetryDelaySeconds < _options.InitialRetryDelaySeconds
            || _options.RetentionDays <= 0
            || _options.CleanupIntervalMinutes <= 0)
        {
            throw new InvalidOperationException("Delivery configuration contains invalid values.");
        }
    }
}
