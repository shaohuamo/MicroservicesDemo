using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Infrastructure.Options;
using StackExchange.Redis;

namespace ProductsMicroservice.Infrastructure.Extensions
{
    internal static class RedisServiceCollectionExtensions
    {
        internal static IServiceCollection AddProductsRedis(this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

            var redisConfig = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            redisConfig.ConnectRetry = redisOptions.ConnectRetry;
            redisConfig.ConnectTimeout = redisOptions.ConnectTimeout;
            redisConfig.SyncTimeout = redisOptions.SyncTimeout;
            redisConfig.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
            redisConfig.ReconnectRetryPolicy = new ExponentialRetry(
                redisOptions.InitialReconnectDelay,
                redisOptions.MaxReconnectDelay
            );

            IConnectionMultiplexer connectionMultiplexer;
            try
            {
                connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfig);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Products Redis connection initialization failed during startup.",
                    exception);
            }

            // Register the interface so OpenTelemetry Redis instrumentation can resolve this instance from DI.
            services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(connectionMultiplexer);
                options.InstanceName = redisOptions.InstanceName;
            });

            services.AddSingleton<IDistributedLockProvider>(_ =>
            {
                var database = connectionMultiplexer.GetDatabase();
                return new RedisDistributedSynchronizationProvider(database);
            });
            services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

            return services;
        }
    }
}
