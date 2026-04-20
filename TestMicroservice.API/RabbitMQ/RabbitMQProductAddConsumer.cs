using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using CommonService.RabbitMQ;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using OpenTelemetry;
using TestMicroservice.API.Diagnostics;
using ProductsMicroservice.Core.MessageQueue.Messages;

namespace TestMicroservice.API.RabbitMQ
{
    public class RabbitMQProductAddConsumer: IRabbitMQProductAddConsumer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQProductAddConsumer> _logger;
        private readonly string _queueName;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly IRabbitMQConnectionProvider _connectionProvider;


        public RabbitMQProductAddConsumer(IConfiguration configuration, ILogger<RabbitMQProductAddConsumer> logger,
            IRabbitMQConnectionProvider connectionProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _queueName = _configuration["RabbitMQ_Products_Queue"]!;
            _connectionProvider = connectionProvider;
        }

        public async Task ConsumeAsync()
        {
            _connection = await _connectionProvider.GetConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            //declare Exchange and Queue
            await DeclareAsync();

            await ConfigureConsumerAsync();
        }

        /// <summary>
        /// declare Exchange and Queue
        /// </summary>
        /// <returns></returns>
        private async Task DeclareAsync()
        {
            string routingKey = _configuration["RabbitMQ_Products_RoutingKey"]!;
            string exchangeName = _configuration["RabbitMQ_Products_Exchange"]!;

            //Create exchange
            await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: true);

            //Create message queue
            await _channel!.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null);

            //Consumer get once only one message
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            //Bind the message to exchange
            await _channel!.QueueBindAsync(queue: _queueName, exchange: exchangeName, routingKey: routingKey);
        }

        /// <summary>
        /// Configure Consumer
        /// </summary>
        /// <returns></returns>
        private async Task ConfigureConsumerAsync()
        {
            //AsyncEventingBasicConsumer : Handles messages received from RabbitMQ
            var consumer = new AsyncEventingBasicConsumer(_channel!);

            //ReceivedAsync: Event triggered when a message is received
            consumer.ReceivedAsync += async (_, ea) =>
            {
                await ConsumeMessageAsync(ea);
            };

            //BasicConsumeAsync: Starts consuming messages from the specified queue
            await _channel!.BasicConsumeAsync(queue: _queueName, consumer: consumer, autoAck: false);
        }

        private async Task ConsumeMessageAsync(BasicDeliverEventArgs ea)
        {
            var stopwatch = Stopwatch.StartNew();

            //010-000:Extract parent trace context (TraceParent and Baggage)from headers
            var parentContext = Propagators.DefaultTextMapPropagator.Extract(
                default,
                ea.BasicProperties,
                (props, key) =>
                {
                    if (props.Headers != null && props.Headers.TryGetValue(key, out var value))
                    {
                        return new[] { Encoding.UTF8.GetString((byte[])value!) };
                    }
                    return Array.Empty<string>();
                });

            //020-000:Set the extracted baggage to the current context for downstream correlation
            Baggage.Current = parentContext.Baggage;

            using var activity = RabbitMQTelemetry.ActivitySource.StartActivity(
                                "rabbitmq.consume",
                                ActivityKind.Consumer,
                                parentContext.ActivityContext);

            //040-000:Set messaging tags for better observability in tracing tools
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", _queueName);
            activity?.SetTag("messaging.operation", "process");
            activity?.SetTag("messaging.rabbitmq.delivery_tag", ea.DeliveryTag);
            activity?.SetTag("messaging.rabbitmq.exchange", ea.Exchange);
            activity?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);

            //050-000:Process the message
            await ProcessMessageAsync(ea, stopwatch, activity);
        }


        #region 050-000:Process the message
        /// <summary>
        /// //050-000:Process the message
        /// </summary>
        /// <param name="ea"></param>
        /// <param name="stopwatch"></param>
        /// <param name="activity"></param>
        /// <returns></returns>
        private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, Stopwatch stopwatch, Activity? activity)
        {
            try
            {
                //050-010:get message data
                var body = ea.Body.ToArray();
                string message = Encoding.UTF8.GetString(body);

                var productAddMessage = JsonSerializer.Deserialize<ProductAddMessage>(message);

                //050-020:validate message data
                if (productAddMessage == null)
                {
                    stopwatch.Stop();

                    _logger.LogWarning("Invalid message received from RabbitMQ");

                    activity?.SetStatus(ActivityStatusCode.Error, "Invalid message");

                    DiagnosticsConfig.RabbitMqConsumeCounter.Add(1,
                        new KeyValuePair<string, object?>("status", "invalid"));

                    // reject bad message (optional: dead-letter)
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
                }

                //050-030:Extract user_id from baggage (set by upstream service)
                var userId = Baggage.GetBaggage("user_id");

                //050-040:Set tags and log context for better traceability
                activity?.SetTag("product.id", productAddMessage!.ProductId);
                activity?.SetTag("messaging.user_id", userId);

                using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"]=productAddMessage!.ProductId }))
                {
                    _logger.LogInformation("Processing RabbitMQ message");

                    // simulate processing
                    _logger.LogInformation("Simulating message processing...");
                    await Task.Delay(3000);

                    stopwatch.Stop();

                    // Metrics
                    DiagnosticsConfig.RabbitMqProcessingHistogram
                        .Record(stopwatch.Elapsed.TotalSeconds);

                    DiagnosticsConfig.RabbitMqConsumeCounter.Add(1,
                        new KeyValuePair<string, object?>("status", "success"));

                    // Trace
                    activity?.SetTag("messaging.process.duration.seconds",
                        stopwatch.Elapsed.TotalSeconds);

                    _logger.LogInformation(
                        "Message processed successfully in {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);
                }

                activity?.SetStatus(ActivityStatusCode.Ok);

                //050-050:ack message after successful processing to remove it from the queue
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "Error processing RabbitMQ message");

                DiagnosticsConfig.RabbitMqConsumeCounter.Add(1,
                    new KeyValuePair<string, object?>("status", "error"));

                activity?.AddException(ex);

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                // requeue or dead-letter depending on strategy
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        } 
        #endregion

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
            {
                await _channel.DisposeAsync();
            }
        }
    }
}
