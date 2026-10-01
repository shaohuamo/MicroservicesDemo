using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductsMicroservice.Infrastructure.Decorators.Caching
{
    public class ProductsDeleterCachingDecorator : IProductsDeleterService
    {
        private readonly IProductsDeleterService _inner;
        private readonly IDistributedCache _cache;
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<ProductsDeleterCachingDecorator> _logger;

        public ProductsDeleterCachingDecorator(
            IProductsDeleterService inner,
            IDistributedCache cache,
            IOptions<CacheOptions> cacheOptions,
            ILogger<ProductsDeleterCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
            _logger = logger;
        }

        public async Task<ProductDeleteResult> DeleteProductAsync(Guid productId, int expectedVersion, Guid idempotencyKey)
        {
            if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            string cacheKey = ProductCacheKeys.GetDetailsKey(productId);

            ProductDeleteResult result;
            // call the inner service to delete the product
            try
            {
                result = await _inner.DeleteProductAsync(productId, expectedVersion, idempotencyKey);
            }
            catch (ProductConcurrencyException)
            {
                await InvalidateCacheAsync(cacheKey, productId);
                throw;
            }
            catch (ProductNotFoundException)
            {
                await InvalidateAfterNotFoundAsync(cacheKey, productId);
                throw;
            }

            if (result.IsReplay) return result;

            // Remove cache after a successful delete.
            await InvalidateCacheAsync(cacheKey, productId);
            return result;
        }

        private async Task InvalidateAfterNotFoundAsync(string cacheKey, Guid productId)
        {
            var activity = Activity.Current;
            activity?.AddEvent(new("Cache Invalidation Start"));

            bool[] results = await Task.WhenAll(
                TryRemoveAsync(ProductCacheKeys.AllProductsKey, activity),
                TryRemovePositiveDetailAsync(cacheKey, activity));
            bool invalidated = results.All(removed => removed);
            activity?.SetTag("cache.invalidated", invalidated);

            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated after ProductId was not found: {ProductId}", productId);
            }
        }

        private async Task InvalidateCacheAsync(string cacheKey, Guid productId)
        {
            var activity = Activity.Current;

            activity?.AddEvent(new("Cache Invalidation Start"));

            bool[] results = await Task.WhenAll(
                TryRemoveAsync(cacheKey, activity),
                TryRemoveAsync(ProductCacheKeys.AllProductsKey, activity));
            bool invalidated = results.All(removed => removed);
            activity?.SetTag("cache.invalidated", invalidated);
            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated for ProductId: {ProductId}", productId);
            }
        }

        private async Task<bool> TryRemovePositiveDetailAsync(string cacheKey, Activity? activity)
        {
            try
            {
                string? cachedDetail = await _cache.GetStringAsync(cacheKey);
                return cachedDetail is null || cachedDetail == _cacheOptions.NullValuePlaceholder ||
                    await TryRemoveAsync(cacheKey, activity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache lookup failed for {CacheKey}", cacheKey);
                activity?.AddException(ex);
                return false;
            }
        }

        private async Task<bool> TryRemoveAsync(string cacheKey, Activity? activity)
        {
            try
            {
                await _cache.RemoveAsync(cacheKey);
                return true;
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                _logger.LogWarning(ex, "Cache invalidation failed for {CacheKey}", cacheKey);
                return false;
            }
        }
    }
}
