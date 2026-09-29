using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain.Exceptions;
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
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<ProductsUpdaterCachingDecorator> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductsUpdaterCachingDecorator(IProductsUpdaterService inner,
            IDistributedCache cache, IOptions<RedisOptions> options, IOptions<CacheOptions> cacheOptions,
            ILogger<ProductsUpdaterCachingDecorator> logger, IServiceScopeFactory scopeFactory)
        {
            _innerService = inner;
            _cache = cache;
            _redisOptions = options.Value;
            _cacheOptions = cacheOptions.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<ProductResponse> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
        {
            ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

            string cacheKey = ProductCacheKeys.GetDetailsKey(productUpdateRequest.ProductId);
            var activity = Activity.Current;

            // 010-000: update the product before invalidating its cache.
            ProductResponse response;
            try
            {
                response = await _innerService.UpdateProductAsync(productUpdateRequest);
            }
            catch (ProductConcurrencyException)
            {
                await InvalidateCacheAsync(cacheKey, activity);
                throw;
            }
            catch (ProductNotFoundException)
            {
                await InvalidateAfterNotFoundAsync(cacheKey, activity);
                throw;
            }

            // 020-000: invalidate cache after a successful update.
            await InvalidateCacheAsync(cacheKey, activity);

            // 030-000: repeat invalidation after the configured delay.
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();

                var scopedCache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ProductsUpdaterCachingDecorator>>();

                try
                {
                    await Task.Delay(_redisOptions.DelayedDeleteMs);
                    bool detailRemoved = await TryRemoveAsync(scopedCache, scopedLogger, cacheKey,
                        LogLevel.Error);
                    bool listRemoved = await TryRemoveAsync(scopedCache, scopedLogger,
                        ProductCacheKeys.AllProductsKey, LogLevel.Error);
                    if (detailRemoved && listRemoved)
                    {
                        scopedLogger.LogInformation("Delayed cache invalidation completed for {ProductId}", response.ProductId);
                    }
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Delayed cache invalidation failed");
                }
            });
            return response;
        }

        private async Task InvalidateAfterNotFoundAsync(string cacheKey, Activity? activity)
        {
            activity?.AddEvent(new("Cache Invalidation Start"));

            bool listRemoved = await TryRemoveAsync(_cache, _logger,
                ProductCacheKeys.AllProductsKey, LogLevel.Warning, activity);
            bool detailLookupSucceeded = false;
            bool removeDetail = false;
            try
            {
                string? cachedDetail = await _cache.GetStringAsync(cacheKey);
                detailLookupSucceeded = true;
                removeDetail = cachedDetail is not null &&
                    cachedDetail != _cacheOptions.NullValuePlaceholder;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache lookup failed for {CacheKey}", cacheKey);
                activity?.AddException(ex);
            }

            bool detailRemoved = !removeDetail ||
                await TryRemoveAsync(_cache, _logger, cacheKey, LogLevel.Warning, activity);
            bool invalidated = detailLookupSucceeded && detailRemoved && listRemoved;
            activity?.SetTag("cache.invalidated", invalidated);

            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated after product was not found");
                activity?.AddEvent(new("Cache Invalidation Success"));
            }
        }

        private async Task InvalidateCacheAsync(string cacheKey, Activity? activity)
        {
            activity?.AddEvent(new("Cache Invalidation Start"));

            bool detailRemoved = await TryRemoveAsync(_cache, _logger, cacheKey, LogLevel.Warning, activity);
            bool listRemoved = await TryRemoveAsync(_cache, _logger,
                ProductCacheKeys.AllProductsKey, LogLevel.Warning, activity);
            bool invalidated = detailRemoved && listRemoved;
            activity?.SetTag("cache.invalidated", invalidated);

            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated successfully");
                activity?.AddEvent(new("Cache Invalidation Success"));
            }
        }

        private static async Task<bool> TryRemoveAsync(IDistributedCache cache, ILogger logger,
            string cacheKey, LogLevel failureLevel, Activity? activity = null)
        {
            try
            {
                await cache.RemoveAsync(cacheKey);
                return true;
            }
            catch (Exception ex)
            {
                logger.Log(failureLevel, ex, "Cache invalidation failed for {CacheKey}", cacheKey);
                activity?.AddException(ex);
                return false;
            }
        }
    }
}
