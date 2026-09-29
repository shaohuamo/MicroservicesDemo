using System.Text.Json;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Core.DTO;
using StackExchange.Redis;

namespace NotificationsMicroservice.Infrastructure.Delivery;

public sealed class RedisPresencePublisher(IConnectionMultiplexer connectionMultiplexer)
    : IPresencePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<string>> GetActiveBffInstancesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // Each user has a sorted set of active connections. The score is the
        // connection heartbeat expiry time in Unix milliseconds.
        var database = connectionMultiplexer.GetDatabase();
        var key = (RedisKey)$"notifications:presence:{userId}";
        var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Remove stale heartbeats first so disconnected clients cannot receive
        // real-time delivery attempts through a previously active BFF instance.
        await database.SortedSetRemoveRangeByScoreAsync(
                key,
                double.NegativeInfinity,
                nowUnixMilliseconds)
            .WaitAsync(cancellationToken);

        // Read only members whose expiry is still in the future. Excluding the
        // start score prevents a heartbeat that expires exactly now from counting as active.
        var members = await database.SortedSetRangeByScoreAsync(
                key,
                nowUnixMilliseconds,
                double.PositiveInfinity,
                Exclude.Start)
            .WaitAsync(cancellationToken);

        // Members use the "{bffInstanceId}:{connectionId}" format. Multiple browser
        // connections can belong to one BFF instance, so extract and de-duplicate the instance IDs.
        return members
            .Select(static value => ExtractBffInstanceId(value.ToString()))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public async Task PublishAsync(
        IReadOnlyCollection<string> bffInstanceIds,
        RealtimeNotificationMessage message,
        CancellationToken cancellationToken)
    {
        if (bffInstanceIds.Count == 0)
        {
            return;
        }

        var subscriber = connectionMultiplexer.GetSubscriber();
        var payload = JsonSerializer.Serialize(message, JsonOptions);

        foreach (var bffInstanceId in bffInstanceIds)
        {
            var channel = RedisChannel.Literal($"notifications:bff:{bffInstanceId}");
            await subscriber.PublishAsync(channel, payload).WaitAsync(cancellationToken);
        }
    }

    private static string? ExtractBffInstanceId(string member)
    {
        var separator = member.LastIndexOf(':');
        return separator > 0 ? member[..separator] : null;
    }
}
