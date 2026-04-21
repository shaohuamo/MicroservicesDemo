using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Options;
using System.Diagnostics;

namespace ProductsMicroservice.Infrastructure.Decorators.Caching
{
    public class ProductsUpdaterCachingDecorator : IProductsUpdaterService
    {
        private readonly IProductsUpdaterService _innerService;
        private readonly IDistributedCache _cache;
        private readonly RedisOptions _redisOptions;
        private readonly ILogger<ProductsUpdaterCachingDecorator> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductsUpdaterCachingDecorator(IProductsUpdaterService inner,
            IDistributedCache cache, IOptions<RedisOptions> options, ILogger<ProductsUpdaterCachingDecorator> logger, IServiceScopeFactory scopeFactory)
        {
            _innerService = inner;
            _cache = cache;
            _redisOptions = options.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<ProductResponse?> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
        {
            ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

            string cacheKey = ProductCacheKeys.GetDetailsKey(productUpdateRequest.ProductId);
            var activity = Activity.Current;

            // 010-000:remove cache before update
            try
            {
                await _cache.RemoveAsync(cacheKey);
                await _cache.RemoveAsync(ProductCacheKeys.AllProductsKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pre-update cache remove failed, continuing...");
            }

            // 020-000:call innerService
            var response = await _innerService.UpdateProductAsync(productUpdateRequest);

            if (response != null)
            {
                // 030-000:remove cache after update (double delete)
                activity?.AddEvent(new("Cache Invalidation Start"));

                try
                {
                    await _cache.RemoveAsync(cacheKey);
                    await _cache.RemoveAsync(ProductCacheKeys.AllProductsKey);

                    _logger.LogInformation("Cache invalidated successfully");
                    activity?.SetTag("cache.invalidated", true);
                    activity?.AddEvent(new("Cache Invalidation Success"));
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Cache invalidation failed");
                    activity?.AddException(cacheEx);
                    activity?.SetTag("cache.invalidated", false);
                }

                // 040-000:delayed cache invalidation
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();

                    var scopedCache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ProductsUpdaterCachingDecorator>>();

                    try
                    {
                        await Task.Delay(_redisOptions.DelayedDeleteMs);
                        await scopedCache.RemoveAsync(cacheKey);
                        await scopedCache.RemoveAsync(ProductCacheKeys.AllProductsKey);
                        scopedLogger.LogInformation("Delayed cache invalidation completed for {ProductId}", response.ProductId);
                    }
                    catch (Exception ex)
                    {
                        scopedLogger.LogError(ex, "Delayed cache invalidation failed");
                    }
                });
            }
            return response;
        }
    }
}
