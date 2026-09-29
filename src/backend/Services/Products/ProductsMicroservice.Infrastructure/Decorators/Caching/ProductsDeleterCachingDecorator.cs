using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain.Exceptions;
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

        public async Task DeleteProductAsync(Guid productId, int expectedVersion)
        {
            if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            string cacheKey = ProductCacheKeys.GetDetailsKey(productId);

            // call the inner service to delete the product
            try
            {
                await _inner.DeleteProductAsync(productId, expectedVersion);
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

            // Remove cache after a successful delete.
            await InvalidateCacheAsync(cacheKey, productId);
        }

        private async Task InvalidateAfterNotFoundAsync(string cacheKey, Guid productId)
        {
            var activity = Activity.Current;
            activity?.AddEvent(new("Cache Invalidation Start"));

            bool listRemoved = await TryRemoveAsync(ProductCacheKeys.AllProductsKey, activity);
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

            bool detailRemoved = !removeDetail || await TryRemoveAsync(cacheKey, activity);
            bool invalidated = detailLookupSucceeded && detailRemoved && listRemoved;
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

            bool detailRemoved = await TryRemoveAsync(cacheKey, activity);
            bool listRemoved = await TryRemoveAsync(ProductCacheKeys.AllProductsKey, activity);
            bool invalidated = detailRemoved && listRemoved;
            activity?.SetTag("cache.invalidated", invalidated);
            if (invalidated)
            {
                _logger.LogInformation("Cache invalidated for ProductId: {ProductId}", productId);
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
