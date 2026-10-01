using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Infrastructure.Delivery;
using NotificationsMicroservice.Infrastructure.Options;
using StackExchange.Redis;

namespace NotificationsMicroservice.Infrastructure.Extensions;

internal static class RedisServiceCollectionExtensions
{
    internal static IServiceCollection AddNotificationsRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            var redisConfiguration = ConfigurationOptions.Parse(redis.ConnectionString);
            redisConfiguration.ConnectRetry = redis.ConnectRetry;
            redisConfiguration.ConnectTimeout = redis.ConnectTimeout;
            redisConfiguration.SyncTimeout = redis.SyncTimeout;
            redisConfiguration.AbortOnConnectFail = redis.AbortOnConnectFail;
            redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
            redisConfiguration.ReconnectRetryPolicy = new ExponentialRetry(
                redis.InitialReconnectDelayMilliseconds,
                redis.MaxReconnectDelayMilliseconds);
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });
        services.AddSingleton<IPresencePublisher, RedisPresencePublisher>();

        return services;
    }
}
