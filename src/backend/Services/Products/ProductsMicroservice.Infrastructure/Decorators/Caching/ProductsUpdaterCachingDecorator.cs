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

        public async Task<ProductUpdateResult> UpdateProductAsync(ProductUpdateRequest productUpdateRequest, Guid idempotencyKey)
        {
            ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

            string cacheKey = ProductCacheKeys.GetDetailsKey(productUpdateRequest.ProductId);
            var activity = Activity.Current;

            // 010-000: update the product before invalidating its cache.
            ProductUpdateResult response;
            try
            {
                response = await _innerService.UpdateProductAsync(productUpdateRequest, idempotencyKey);
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

            if (response.IsReplay) return response;

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
                    bool[] results = await Task.WhenAll(
                        TryRemoveAsync(scopedCache, scopedLogger, cacheKey, LogLevel.Error),
                        TryRemoveAsync(scopedCache, scopedLogger,
                            ProductCacheKeys.AllProductsKey, LogLevel.Error));
                    if (results.All(removed => removed))
                    {
                        scopedLogger.LogInformation("Delayed cache invalidation completed for {ProductId}", response.Product.ProductId);
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

            bool[] results = await Task.WhenAll(
                TryRemoveAsync(_cache, _logger, ProductCacheKeys.AllProductsKey,
                    LogLevel.Warning, activity),
                TryRemovePositiveDetailAsync(cacheKey, activity));
            bool invalidated = results.All(removed => removed);
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

            bool[] results = await Task.WhenAll(
                TryRemoveAsync(_cache, _logger, cacheKey, LogLevel.Warning, activity),
                TryRemoveAsync(_cache, _logger, ProductCacheKeys.AllProductsKey,
                    LogLevel.Warning, activity));
            bool invalidated = results.All(removed => removed);
            activity?.SetTag("cache.invalidated", invalidated);

            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated successfully");
                activity?.AddEvent(new("Cache Invalidation Success"));
            }
        }

        private async Task<bool> TryRemovePositiveDetailAsync(string cacheKey, Activity? activity)
        {
            try
            {
                string? cachedDetail = await _cache.GetStringAsync(cacheKey);
                return cachedDetail is null || cachedDetail == _cacheOptions.NullValuePlaceholder ||
                    await TryRemoveAsync(_cache, _logger, cacheKey, LogLevel.Warning, activity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache lookup failed for {CacheKey}", cacheKey);
                activity?.AddException(ex);
                return false;
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
