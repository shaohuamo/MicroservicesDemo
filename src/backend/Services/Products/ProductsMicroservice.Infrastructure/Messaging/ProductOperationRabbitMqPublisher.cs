using System.Diagnostics;
using System.Text;
using CommonService.RabbitMQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using ProductsMicroservice.Infrastructure.Options;
using RabbitMQ.Client;

namespace ProductsMicroservice.Infrastructure.Messaging;

internal sealed class ProductOperationRabbitMqPublisher
{
    private readonly IRabbitMQConnectionProvider _connectionProvider;
    private readonly ProductOperationMessagingOptions _options;
    private readonly ILogger<ProductOperationRabbitMqPublisher> _logger;

    public ProductOperationRabbitMqPublisher(
        IRabbitMQConnectionProvider connectionProvider,
        IOptions<ProductOperationMessagingOptions> options,
        ILogger<ProductOperationRabbitMqPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid notificationId,
        string payload,
        ActivityContext parentContext,
        CancellationToken cancellationToken)
    {
        IConnection connection = await _connectionProvider.GetConnectionAsync();
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using IChannel channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        string messageId = notificationId.ToString("D");
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = "product.operation.completed",
            MessageId = messageId,
            Headers = new Dictionary<string, object?>
            {
                ["message_id"] = Encoding.UTF8.GetBytes(messageId)
            }
        };
        var returned = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        channel.BasicReturnAsync += (_, eventArgs) =>
        {
            if (string.Equals(eventArgs.BasicProperties.MessageId, messageId, StringComparison.Ordinal))
            {
                returned.TrySetResult(
                    $"RabbitMQ returned the message as unroutable ({eventArgs.ReplyCode}: {eventArgs.ReplyText}).");
            }

            return Task.CompletedTask;
        };

        using Activity? activity = RabbitMQTelemetry.ActivitySource.StartActivity(
            "product-operation.publish", ActivityKind.Producer, parentContext);
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activity?.Context ?? parentContext, Baggage.Current),
            properties,
            static (carrier, key, value) =>
            {
                carrier.Headers ??= new Dictionary<string, object?>();
                carrier.Headers[key] = Encoding.UTF8.GetBytes(value);
            });
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _options.ExchangeName);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", _options.RoutingKey);
        activity?.SetTag("messaging.message.id", messageId);

        try
        {
            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: _options.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payload),
                cancellationToken: cancellationToken);

            // RabbitMQ sends basic.return before the publisher confirmation for
            // a mandatory unroutable message. A confirm alone therefore is not
            // sufficient to mark the durable outbox record as published.
            if (returned.Task.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(returned.Task.Result);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogWarning(exception,
                "RabbitMQ did not confirm product notification {NotificationId}", notificationId);
            throw;
        }
    }
}
