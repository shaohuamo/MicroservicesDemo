using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Domain.Exceptions;
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

    public async Task<ProductDeleteResult> DeleteProductAsync(Guid productId, int expectedVersion, Guid idempotencyKey)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));
        }

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag("idempotency.key", idempotencyKey.ToString("D"));
        activity?.SetTag("idempotency.operation", "DeleteProduct");

        // Trace Instrumentation
        activity?.AddEvent(new("Remove Product By ProductId"));
        activity?.SetTag("product.id", productId);

        using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
        {
            try
            {
                _logger.LogInformation("Product deletion started");
                ProductDeleteResult result = await _inner.DeleteProductAsync(productId, expectedVersion, idempotencyKey);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.DeleteProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);
                if (!result.IsReplay) DiagnosticsConfig.ProductsCounter.Add(-1,
                    new KeyValuePair<string, object?>("product.id", productId),
                    new("status", "success"));

                // Trace Instrumentation
                activity?.SetTag("product.deleted", !result.IsReplay);
                activity?.SetTag("product.operation.committed", true);
                activity?.SetTag("idempotency.outcome", result.IsReplay ? "replayed" : "deleted");
                activity?.SetTag("idempotency.replayed", result.IsReplay);
                activity?.SetTag("idempotency.source", result.Source.ToString().ToLowerInvariant());
                activity?.AddEvent(new("Product Deletion Finished"));
                _logger.LogInformation(
                    "Product operation {Outcome} in {ElapsedMs} ms",
                    result.IsReplay ? "replayed" : "committed", stopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                if (ex is IdempotencyPayloadConflictException)
                {
                    activity?.SetTag("idempotency.outcome", "payload_conflict");
                    activity?.SetTag("idempotency.replayed", false);
                }
                _logger.LogError(ex, "Uncaught error during product deletion for {ProductId}", productId);
                // Trace Instrumentation
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                throw;
            }
        }
    }
}
