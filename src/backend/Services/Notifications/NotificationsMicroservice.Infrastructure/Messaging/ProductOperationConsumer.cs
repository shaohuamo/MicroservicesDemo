using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonService.Messages;
using CommonService.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Options;
using NotificationsMicroservice.Infrastructure.Health;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationsMicroservice.Infrastructure.Messaging;

public sealed class ProductOperationConsumer(
    IRabbitMQConnectionProvider connectionProvider,
    INotificationAddRepository repository,
    IOptions<ProductOperationsOptions> options,
    IProductOperationConsumerHealthState healthState,
    ILogger<ProductOperationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ProductOperationsOptions _options = options.Value;
    private IChannel? _channel;
    private IConnection? _observedConnection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                healthState.MarkNotReady("RabbitMQ consumer is connecting.");
                await StartConsumerAsync(stoppingToken);
                healthState.MarkReady();
                logger.LogInformation(
                    "Consuming product operation notifications from {Queue}.",
                    _options.Queue);

                while (!stoppingToken.IsCancellationRequested && _channel is { IsOpen: true })
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    throw new InvalidOperationException("RabbitMQ consumer channel closed.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                healthState.MarkNotReady("RabbitMQ notification consumer is retrying.");
                await DisposeChannelAsync();
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 30) + Random.Shared.NextDouble());
                logger.LogWarning(
                    exception,
                    "RabbitMQ notification consumer start attempt {Attempt} failed; retrying in {DelaySeconds:n1}s.",
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        healthState.MarkNotReady("RabbitMQ consumer is stopping.");
        await base.StopAsync(cancellationToken);
        await DisposeChannelAsync();
    }

    private async Task StartConsumerAsync(CancellationToken cancellationToken)
    {
        ValidateOptions();
        var connection = await connectionProvider.GetConnectionAsync();
        if (!ReferenceEquals(_observedConnection, connection))
        {
            _observedConnection = connection;
            connection.ConnectionShutdownAsync += (_, _) =>
            {
                healthState.MarkNotReady("RabbitMQ connection is closed.");
                return Task.CompletedTask;
            };
            connection.RecoverySucceededAsync += (_, _) =>
            {
                if (_channel is { IsOpen: true })
                {
                    healthState.MarkReady();
                }

                return Task.CompletedTask;
            };
        }
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channel.ChannelShutdownAsync += (_, _) =>
        {
            healthState.MarkNotReady("RabbitMQ consumer channel is closed.");
            return Task.CompletedTask;
        };

        await _channel.ExchangeDeclareAsync(
            _options.Exchange,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken);
        await _channel.ExchangeDeclareAsync(
            _options.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(
            _options.DeadLetterQueue,
            _options.DeadLetterExchange,
            _options.RoutingKey,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-message-ttl"] = 10_000,
            ["x-delivery-limit"] = 3,
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _options.RoutingKey
        };
        await _channel.QueueDeclareAsync(
            _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(
            _options.Queue,
            _options.Exchange,
            _options.RoutingKey,
            cancellationToken: cancellationToken);
        await _channel.BasicQosAsync(0, _options.PrefetchCount, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, eventArgs) => ProcessMessageAsync(eventArgs, stoppingToken: cancellationToken);
        await _channel.BasicConsumeAsync(
            _options.Queue,
            autoAck: false,
            consumer,
            cancellationToken: cancellationToken);
    }

    private async Task ProcessMessageAsync(
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            return;
        }

        var parentContext = Propagators.DefaultTextMapPropagator.Extract(
            default,
            eventArgs.BasicProperties,
            static (properties, key) => ExtractHeader(properties, key));
        Baggage.Current = parentContext.Baggage;

        using var activity = RabbitMQTelemetry.ActivitySource.StartActivity(
            "products.operation.consume",
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _options.Queue);
        activity?.SetTag("messaging.rabbitmq.exchange", eventArgs.Exchange);
        activity?.SetTag("messaging.rabbitmq.routing_key", eventArgs.RoutingKey);

        try
        {
            var body = eventArgs.Body;
            var messageId = eventArgs.BasicProperties.MessageId;
            var message = JsonSerializer.Deserialize<ProductOperationResultMessage>(body.Span, JsonOptions);

            if (message is null || !IsValid(message, messageId))
            {
                logger.LogWarning("Dead-lettering invalid product operation message {MessageId}.", messageId);
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid product operation message");
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }

            var payloadHash = Convert.ToHexString(SHA256.HashData(body.Span));
            var notification = new ProductOperationNotification(
                message.NotificationId,
                message.OccurredAtUtc,
                message.CorrelationId,
                message.UserId,
                message.UserEmail,
                message.Culture,
                message.Operation.ToString(),
                message.Status.ToString(),
                message.ProductId,
                message.ProductName,
                message.ProductVersion,
                message.ErrorCode,
                activity?.Id,
                activity?.TraceStateString);

            var result = await repository.StoreAsync(notification, payloadHash, stoppingToken);
            if (result == NotificationStoreResult.PayloadConflict)
            {
                logger.LogError(
                    "Dead-lettering message {MessageId}: NotificationId was reused with a different payload.",
                    messageId);
                activity?.SetStatus(ActivityStatusCode.Error, "NotificationId payload conflict");
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }

            activity?.SetTag("messaging.message.id", messageId);
            activity?.SetTag("notification.id", message.NotificationId);
            activity?.SetTag("notification.duplicate", result == NotificationStoreResult.Duplicate);
            activity?.SetStatus(ActivityStatusCode.Ok);
            await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Dead-lettering malformed product operation JSON message.");
            activity?.SetStatus(ActivityStatusCode.Error, "Malformed JSON");
            await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // RabbitMQ will redeliver an unacknowledged delivery when the channel closes.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Product operation message persistence failed; requeueing delivery.");
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, "Persistence failed");
            await _channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: true, stoppingToken);
        }
    }

    private static bool IsValid(ProductOperationResultMessage message, string? transportMessageId)
    {
        return message.NotificationId != Guid.Empty
               && Guid.TryParse(transportMessageId, out var parsedMessageId)
               && parsedMessageId == message.NotificationId
               && message.OccurredAtUtc != default
               && !string.IsNullOrWhiteSpace(message.CorrelationId)
               && !string.IsNullOrWhiteSpace(message.UserId)
               && !string.IsNullOrWhiteSpace(message.UserEmail)
               && !string.IsNullOrWhiteSpace(message.Culture)
               && message.ProductId != Guid.Empty
               && Enum.IsDefined(message.Operation)
               && Enum.IsDefined(message.Status);
    }

    private static IEnumerable<string> ExtractHeader(IReadOnlyBasicProperties properties, string key)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            byte[] bytes => [Encoding.UTF8.GetString(bytes)],
            ReadOnlyMemory<byte> bytes => [Encoding.UTF8.GetString(bytes.Span)],
            _ => [value?.ToString() ?? string.Empty]
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Exchange)
            || string.IsNullOrWhiteSpace(_options.RoutingKey)
            || string.IsNullOrWhiteSpace(_options.Queue)
            || string.IsNullOrWhiteSpace(_options.DeadLetterExchange)
            || string.IsNullOrWhiteSpace(_options.DeadLetterQueue))
        {
            throw new InvalidOperationException("ProductOperations RabbitMQ configuration is incomplete.");
        }
    }

    private async Task DisposeChannelAsync()
    {
        if (_channel is null)
        {
            return;
        }

        await _channel.DisposeAsync();
        _channel = null;
    }
}
