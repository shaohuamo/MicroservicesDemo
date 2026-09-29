using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace NotificationsMicroservice.DlqReplay;

internal static class Program
{
    private const string ReplayCountHeader = "manual-replay-count";
    private static readonly HashSet<string> BrokerHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-death", "x-delivery-count", "x-acquired-count",
        "x-first-death-exchange", "x-first-death-queue", "x-first-death-reason",
        "x-last-death-exchange", "x-last-death-queue", "x-last-death-reason"
    };

    private static async Task<int> Main()
    {
        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            string host = Required(configuration, "RabbitMQ:HostName");
            string user = Required(configuration, "RabbitMQ:UserName");
            string password = Required(configuration, "RabbitMQ:Password");
            int port = PositiveInt(configuration, "RabbitMQ:Port");
            string exchange = Required(configuration, "ProductOperations:Exchange");
            string routingKey = Required(configuration, "ProductOperations:RoutingKey");
            string deadLetterQueue = Required(configuration, "ProductOperations:DeadLetterQueue");
            int batchSize = PositiveInt(configuration, "DlqReplay:BatchSize");
            int maxRunSeconds = PositiveInt(configuration, "DlqReplay:MaxRunSeconds");
            int maxReplayCount = PositiveInt(configuration, "DlqReplay:MaxReplayCount");

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(maxRunSeconds));
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = password,
                AutomaticRecoveryEnabled = false
            };
            await using IConnection connection = await factory.CreateConnectionAsync(deadline.Token);
            await using IChannel consumeChannel = await connection.CreateChannelAsync(cancellationToken: deadline.Token);
            var confirmOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using IChannel publishChannel = await connection.CreateChannelAsync(confirmOptions, deadline.Token);

            var seenThisRun = new HashSet<string>(StringComparer.Ordinal);
            int fetched = 0;
            int replayed = 0;
            while (fetched < batchSize && !deadline.IsCancellationRequested)
            {
                BasicGetResult? delivery = await consumeChannel.BasicGetAsync(
                    deadLetterQueue, autoAck: false, deadline.Token);
                if (delivery is null)
                    break;

                fetched++;
                string? messageId = delivery.BasicProperties.MessageId;
                if (string.IsNullOrWhiteSpace(messageId) || !Guid.TryParse(messageId, out _))
                {
                    Console.WriteLine("Skipped dead letter without a valid MessageId.");
                    continue;
                }
                if (!seenThisRun.Add(messageId))
                {
                    Console.WriteLine($"Skipped {messageId}: already replayed in this run.");
                    continue;
                }

                string? reason = HeaderText(delivery.BasicProperties.Headers, "x-last-death-reason")
                    ?? HeaderText(delivery.BasicProperties.Headers, "x-first-death-reason");
                if (reason is not ("expired" or "delivery_limit"))
                {
                    Console.WriteLine($"Skipped {messageId}: dead-letter reason '{reason ?? "unknown"}' requires review.");
                    continue;
                }

                if (!TryReplayCount(delivery.BasicProperties.Headers, out int replayCount))
                {
                    Console.WriteLine($"Skipped {messageId}: invalid {ReplayCountHeader} header.");
                    continue;
                }
                if (replayCount >= maxReplayCount)
                {
                    Console.WriteLine($"Skipped {messageId}: replay limit {maxReplayCount} reached.");
                    continue;
                }

                var properties = ReplayProperties(delivery.BasicProperties, replayCount + 1);
                await publishChannel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: delivery.Body,
                    cancellationToken: deadline.Token);
                await consumeChannel.BasicAckAsync(delivery.DeliveryTag, multiple: false, deadline.Token);
                replayed++;
                Console.WriteLine($"Replayed {messageId}; manual replay count is {replayCount + 1}.");
            }

            Console.WriteLine($"Completed: fetched {fetched}, replayed {replayed}.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Replay deadline reached. Unacknowledged dead letters remain in the queue.");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Replay failed: {exception}");
            return 1;
        }
    }

    private static BasicProperties ReplayProperties(IReadOnlyBasicProperties original, int replayCount)
    {
        var headers = original.Headers is null
            ? new Dictionary<string, object?>()
            : original.Headers.Where(pair => !BrokerHeaders.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        headers[ReplayCountHeader] = replayCount;

        return new BasicProperties
        {
            Persistent = true,
            MessageId = original.MessageId,
            CorrelationId = original.CorrelationId,
            ContentType = original.ContentType,
            ContentEncoding = original.ContentEncoding,
            Type = original.Type,
            Headers = headers
        };
    }

    private static bool TryReplayCount(IDictionary<string, object?>? headers, out int count)
    {
        count = 0;
        if (headers is null || !headers.TryGetValue(ReplayCountHeader, out object? value))
            return true;

        return value switch
        {
            byte number => Set(number, out count),
            short number when number >= 0 => Set(number, out count),
            int number when number >= 0 => Set(number, out count),
            long number when number >= 0 && number <= int.MaxValue => Set((int)number, out count),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.None,
                CultureInfo.InvariantCulture, out int number) => Set(number, out count),
            ReadOnlyMemory<byte> bytes when int.TryParse(Encoding.UTF8.GetString(bytes.Span), NumberStyles.None,
                CultureInfo.InvariantCulture, out int number) => Set(number, out count),
            string text when int.TryParse(text, NumberStyles.None,
                CultureInfo.InvariantCulture, out int number) => Set(number, out count),
            _ => false
        };
    }

    private static bool Set(int value, out int count)
    {
        count = value;
        return true;
    }

    private static string? HeaderText(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out object? value))
            return null;
        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> bytes => Encoding.UTF8.GetString(bytes.Span),
            string text => text,
            _ => value?.ToString()
        };
    }

    private static string Required(IConfiguration config, string key) =>
        !string.IsNullOrWhiteSpace(config[key])
            ? config[key]!
            : throw new InvalidOperationException($"Configuration '{key}' is required.");

    private static int PositiveInt(IConfiguration config, string key) =>
        int.TryParse(config[key], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : throw new InvalidOperationException($"Configuration '{key}' must be a positive integer.");
}
