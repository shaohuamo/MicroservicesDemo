using System.Diagnostics;
using CommonService.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Infrastructure.Messaging;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;
using ProductsMicroservice.Infrastructure.Options;

namespace ProductsMicroservice.Infrastructure.HostedServices;

internal sealed class ProductOperationOutboxDispatcher : BackgroundService
{
    private readonly IProductOperationOutboxStore _outboxStore;
    private readonly ProductOperationRabbitMqPublisher _publisher;
    private readonly ProductOperationOutboxOptions _options;
    private readonly ILogger<ProductOperationOutboxDispatcher> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public ProductOperationOutboxDispatcher(
        IProductOperationOutboxStore outboxStore,
        ProductOperationRabbitMqPublisher publisher,
        IOptions<ProductOperationOutboxOptions> options,
        ILogger<ProductOperationOutboxDispatcher> logger)
    {
        _outboxStore = outboxStore;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<ProductOperationOutbox> outboxEntries =
                    await _outboxStore.ClaimBatchAsync(
                        _workerId,
                        Math.Clamp(_options.BatchSize, 1, 500),
                        TimeSpan.FromSeconds(Math.Max(_options.LeaseSeconds, 5)),
                        stoppingToken);

                foreach (ProductOperationOutbox outbox in outboxEntries)
                {
                    await DispatchAsync(outbox, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Product operation outbox dispatch cycle failed");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Max(_options.PollIntervalMilliseconds, 100)),
                stoppingToken);
        }
    }

    private async Task DispatchAsync(
        ProductOperationOutbox outbox,
        CancellationToken cancellationToken)
    {
        var parentContext = ActivityContext.TryParse(
            outbox.TraceParent,
            outbox.TraceState,
            isRemote: true,
            out var parsedContext)
            ? parsedContext
            : default;

        using Activity? dispatchActivity = RabbitMQTelemetry.ActivitySource.StartActivity(
            "products.operation.outbox.dispatch",
            ActivityKind.Producer,
            parentContext);
        dispatchActivity?.SetTag("notification.id", outbox.NotificationId);

        try
        {
            await _publisher.PublishAsync(
                outbox.NotificationId,
                outbox.Payload,
                parentContext,
                cancellationToken);
            bool marked = await _outboxStore.MarkPublishedAsync(
                outbox, _workerId, cancellationToken);
            if (!marked)
            {
                _logger.LogWarning(
                    "Lost outbox lease after publishing notification {NotificationId}; duplicate delivery is possible",
                    outbox.NotificationId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await ScheduleRetryAsync(outbox, exception, cancellationToken);
        }
    }

    private async Task ScheduleRetryAsync(
        ProductOperationOutbox outbox,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int attempt = outbox.AttemptCount + 1;
        int maximumDelay = Math.Max(_options.MaxRetryDelaySeconds, 1);
        double delaySeconds = Math.Min(Math.Pow(2, Math.Min(attempt - 1, 20)), maximumDelay);
        string error = exception.GetType().Name + ": " + exception.Message;
        error = error[..Math.Min(error.Length, 512)];

        await _outboxStore.ScheduleRetryAsync(
            outbox,
            _workerId,
            attempt,
            TimeSpan.FromSeconds(delaySeconds + Random.Shared.NextDouble()),
            error,
            cancellationToken);

        _logger.LogWarning(exception,
            "Scheduled outbox notification {NotificationId} retry {Attempt}",
            outbox.NotificationId, attempt);
    }
}
