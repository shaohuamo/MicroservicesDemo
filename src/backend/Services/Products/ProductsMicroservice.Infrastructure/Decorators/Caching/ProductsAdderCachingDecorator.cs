using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.Diagnostics;

namespace ProductsMicroservice.Infrastructure.Decorators.Caching
{
    public class ProductsAdderCachingDecorator : IProductsAdderService
    {
        private readonly IProductsAdderService _inner;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProductsAdderCachingDecorator> _logger;

        public ProductsAdderCachingDecorator(
            IProductsAdderService inner,
            IDistributedCache cache,
            ILogger<ProductsAdderCachingDecorator> logger)
        {
            _inner = inner;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ProductResponse?> AddProductAsync(ProductAddRequest productAddRequest)
        {
            ArgumentNullException.ThrowIfNull(productAddRequest);

            var result = await _inner.AddProductAsync(productAddRequest);

            if (result != null)
            {
                var activity = Activity.Current;
                activity?.AddEvent(new("Cache Invalidation Start"));

                try
                {
                    await _cache.RemoveAsync(ProductCacheKeys.AllProductsKey);
                    activity?.SetTag("cache.invalidated", true);
                    _logger.LogInformation("All-products cache invalidated after adding ProductId: {ProductId}", result.ProductId);
                }
                catch (Exception ex)
                {
                    activity?.AddException(ex);
                    activity?.SetTag("cache.invalidated", false);
                    _logger.LogWarning(ex, "Cache invalidation failed after adding product");
                }
            }

            return result;
        }
    }
}
