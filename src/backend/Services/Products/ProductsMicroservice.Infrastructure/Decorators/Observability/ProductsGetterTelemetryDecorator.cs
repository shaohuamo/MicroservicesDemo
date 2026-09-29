using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability;

public class ProductsGetterTelemetryDecorator : IProductsGetterService
{
    private readonly IProductsGetterService _innerService;
    private readonly ILogger<ProductsGetterTelemetryDecorator> _logger;

    public ProductsGetterTelemetryDecorator(
        IProductsGetterService inner,
        ILogger<ProductsGetterTelemetryDecorator> logger)
    {
        _innerService = inner;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductResponse?>> GetProductsAsync()
    {
        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

        // Trace Instrumentation
        activity?.AddEvent(new("Fetch All Products Start"));

        using (_logger.BeginScope(new Dictionary<string, object> { ["CacheKey"] = ProductCacheKeys.AllProductsKey }))
        {
            try
            {
                _logger.LogInformation("Fetching products flow started");
                IEnumerable<ProductResponse?> result = await _innerService.GetProductsAsync();
                stopwatch.Stop();

                int count = result.Count();
                // Metric Instrumentation
                DiagnosticsConfig.GetProductsHistogram.Record(stopwatch.Elapsed.TotalSeconds);

                // Trace Instrumentation
                activity?.SetTag("products.count", count);
                _logger.LogInformation(
                    "Fetching products flow completed. Count: {ProductCount}; Elapsed: {ElapsedMs} ms",
                    count, stopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error occurred while fetching products flow");
                // Trace Instrumentation
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                throw;
            }
        }
    }

    public async Task<ProductResponse?> GetProductByProductIdAsync(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));
        }

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

        // Trace Instrumentation
        activity?.SetTag("product.id", productId);
        activity?.AddEvent(new("Fetch Product By ProductId"));

        using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
        {
            try
            {
                _logger.LogInformation("Fetching product by ID flow started");
                ProductResponse? result = await _innerService.GetProductByProductIdAsync(productId);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.GetProductByProductIdHistogram.Record(stopwatch.Elapsed.TotalSeconds);

                // Trace Instrumentation
                activity?.SetTag("product.found", result is not null);
                if (result is null)
                {
                    _logger.LogWarning("Product was not found in the flow after {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                else
                {
                    _logger.LogInformation("Product fetched successfully in {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error occurred while fetching product by ID");
                // Trace Instrumentation
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}
