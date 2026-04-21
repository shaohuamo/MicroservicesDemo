using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.ServiceContracts;
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
        private readonly ILogger<ProductsDeleterCachingDecorator> _logger;

        public ProductsDeleterCachingDecorator(
            IProductsDeleterService inner,
            IDistributedCache cache,
            ILogger<ProductsDeleterCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> DeleteProductAsync(Guid productId)
        {
            if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            // call the inner service to delete the product
            bool result = await _inner.DeleteProductAsync(productId);

            //remove cache
            if (result)
            {
                var activity = Activity.Current;
                string cacheKey = ProductCacheKeys.GetDetailsKey(productId);

                activity?.AddEvent(new("Cache Invalidation Start"));

                try
                {
                    await _cache.RemoveAsync(cacheKey);
                    await _cache.RemoveAsync(ProductCacheKeys.AllProductsKey);
                    activity?.SetTag("cache.invalidated", true);
                    _logger.LogInformation("Cache invalidated for ProductId: {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    activity?.AddException(ex);
                    activity?.SetTag("cache.invalidated", false);
                    _logger.LogWarning(ex, "Cache invalidation failed");
                }
            }

            return result;
        }
    }
}
