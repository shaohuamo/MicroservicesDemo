using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Redis;

namespace ProductsMicroservice.Infrastructure.Extensions
{
    internal static class RedisServiceCollectionExtensions
    {
        internal static IServiceCollection AddProductsRedis(this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

            services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
            services.AddSingleton<IProductsRedisConnectionProvider, ProductsRedisConnectionProvider>();
            services.AddSingleton<IProductsRedisLockFactory, ProductsRedisLockFactory>();
            services.AddStackExchangeRedisCache(options => options.InstanceName = redisOptions.InstanceName);
            services.AddOptions<RedisCacheOptions>()
                .Configure<IProductsRedisConnectionProvider>((options, connections) =>
                    options.ConnectionMultiplexerFactory = connections.GetConnectionAsync);
            services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

            return services;
        }
    }
}
