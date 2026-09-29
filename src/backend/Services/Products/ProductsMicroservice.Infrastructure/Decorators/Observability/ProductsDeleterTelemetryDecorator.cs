using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability;

public class ProductsDeleterTelemetryDecorator : IProductsDeleterService
{
    private readonly IProductsDeleterService _inner;
    private readonly ILogger<ProductsDeleterTelemetryDecorator> _logger;

    public ProductsDeleterTelemetryDecorator(
        IProductsDeleterService inner,
        ILogger<ProductsDeleterTelemetryDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task DeleteProductAsync(Guid productId, int expectedVersion)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));
        }

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

        // Trace Instrumentation
        activity?.AddEvent(new("Remove Product By ProductId"));
        activity?.SetTag("product.id", productId);

        using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
        {
            try
            {
                _logger.LogInformation("Product deletion started");
                await _inner.DeleteProductAsync(productId, expectedVersion);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.DeleteProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);
                DiagnosticsConfig.ProductsCounter.Add(-1,
                    new KeyValuePair<string, object?>("product.id", productId),
                    new("status", "success"));

                // Trace Instrumentation
                activity?.SetTag("db.result", "success");
                activity?.SetTag("product.operation.committed", true);
                activity?.AddEvent(new("Product Deletion Finished"));
                _logger.LogInformation(
                    "Product and its outbox notification committed in {ElapsedMs} ms",
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Uncaught error during product deletion for {ProductId}", productId);
                // Trace Instrumentation
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                throw;
            }
        }
    }
}
