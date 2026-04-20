using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.Diagnostics;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability
{
    public class ProductsGetterTelemetryDecorator : IProductsGetterService
    {
        private readonly IProductsGetterService _innerService;
        private readonly ILogger<ProductsGetterTelemetryDecorator> _logger;

        public ProductsGetterTelemetryDecorator(IProductsGetterService inner, 
            ILogger<ProductsGetterTelemetryDecorator> logger)
        {
            _innerService = inner; _logger = logger;
        }

        public async Task<IEnumerable<ProductResponse?>> GetProductsAsync()
        {
            var activity = Activity.Current;
            activity?.AddEvent(new("Fetch All Products Start"));

            using (_logger.BeginScope(new Dictionary<string, object> { ["CacheKey"] = ProductCacheKeys.AllProductsKey }))
            {
                try
                {
                    _logger.LogInformation("Fetching products flow started.");

                    // call inner service
                    var result = await _innerService.GetProductsAsync();

                    var count = result?.Count();

                    _logger.LogInformation("Fetching products flow completed. Count: {ProductCount}", count);
                    activity?.SetTag("products.count", count);

                    return result!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while fetching products flow");

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.AddException(ex);
                    throw;
                }
            }
        }

        public async Task<ProductResponse?> GetProductByProductIdAsync(Guid productId)
        {
            // Defensive programming: validate input parameters
            if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            var activity = Activity.Current;
            activity?.SetTag("product.id", productId);
            activity?.AddEvent(new("Fetch Product By ProductId"));

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
            {
                try
                {
                    _logger.LogInformation("Fetching product by ID flow started.");

                    // call inner service
                    var result = await _innerService.GetProductByProductIdAsync(productId);

                    if (result == null)
                    {
                        _logger.LogWarning("Product was not found in the flow.");
                    }
                    else
                    {
                        _logger.LogInformation("Product fetched successfully.");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while fetching product by ID.");

                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;
                }
            }
        }
    }
}
