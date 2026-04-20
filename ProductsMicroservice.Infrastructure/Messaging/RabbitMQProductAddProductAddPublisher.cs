using System.Diagnostics;
using System.Text.Json;
using System.Text;
using CommonService.RabbitMQ;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.MessageQueue.Abstractions;

namespace ProductsMicroservice.Core.RabbitMQ;

public class RabbitMQProductAddProductAddPublisher : IProductMessagePublisher
{
    private readonly IRabbitMQConnectionProvider _connectionProvider;
    private readonly string _exchangeName;
    private readonly ILogger<RabbitMQProductAddProductAddPublisher> _logger;

    public RabbitMQProductAddProductAddPublisher(IConfiguration configuration, IRabbitMQConnectionProvider connectionProvider, ILogger<RabbitMQProductAddProductAddPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
        _exchangeName = configuration["RabbitMQ_Products_Exchange"]!;
    }


    public async Task PublishAsync<T>(string routingKey, T message)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        await using var channel = await connection.CreateChannelAsync();

        string messageJson = JsonSerializer.Serialize(message);
        byte[] messageBodyInBytes = Encoding.UTF8.GetBytes(messageJson);

        //Create exchange
        //this exchange will be created automatically if not exist
        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Direct, durable: true);

        //Publish message
        using Activity? activity = await PublishMessageAsync(routingKey, channel, messageBodyInBytes);
    }

    private async Task<Activity?> PublishMessageAsync(string routingKey, IChannel channel, byte[] messageBodyInBytes)
    {
        //010-000:Create activity (span)
        var activity = RabbitMQTelemetry.ActivitySource.StartActivity(
            "rabbitmq.publish",
            ActivityKind.Producer);

        //020-000:Set messaging tags
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", _exchangeName);
        activity?.SetTag("messaging.destination_kind", "exchange");
        activity?.SetTag("messaging.rabbitmq.routing_key", routingKey);

        //030-000:Create basic properties and inject trace context into message header
        var properties = new BasicProperties
        {
            Persistent = true,//persist message
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>()
        };

        //030-010 get and set user_id
        // add custom header (for downstream services)
        // get user_id from baggage (set by upstream service) and
        // add it to message header for downstream service to consume
        var userId = Baggage.GetBaggage("user_id");

        if (!string.IsNullOrEmpty(userId))
        {
            activity?.SetTag("messaging.user_id", userId);
            //also set user_id to message header for downstream service to consume
            properties.Headers["user_id"] = Encoding.UTF8.GetBytes(userId);
        }

        //030-020:inject trace context into headers
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activity?.Context ?? default, Baggage.Current),
            properties,
            (props, key, value) =>
            {
                props.Headers![key] = Encoding.UTF8.GetBytes(value);
            });

        //040-000:publish message with logging and error handling
        try
        {
            _logger.LogInformation("Publishing message to RabbitMQ exchange {Exchange} with routing key {RoutingKey}",
                _exchangeName, routingKey);

            //Publish message
            await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: routingKey, mandatory: false,
                basicProperties: properties, body: messageBodyInBytes);
            _logger.LogInformation("Message successfully published to {Exchange} with routing key {RoutingKey}",
                _exchangeName, routingKey);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to publish message to {Exchange} with routing key {RoutingKey}",
                _exchangeName, routingKey);

            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            throw;
        }

        return activity;
    }
}