using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability;

public class ProductsUpdaterTelemetryDecorator : IProductsUpdaterService
{
    private readonly IProductsUpdaterService _innerService;
    private readonly ILogger<ProductsUpdaterTelemetryDecorator> _logger;

    public ProductsUpdaterTelemetryDecorator(
        IProductsUpdaterService innerService,
        ILogger<ProductsUpdaterTelemetryDecorator> logger)
    {
        _innerService = innerService;
        _logger = logger;
    }

    public async Task<ProductUpdateResult> UpdateProductAsync(ProductUpdateRequest productUpdateRequest, Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag("idempotency.key", idempotencyKey.ToString("D"));
        activity?.SetTag("idempotency.operation", "UpdateProduct");

        // Trace Instrumentation
        activity?.AddEvent(new("Update Product"));
        activity?.SetTag("product.id", productUpdateRequest.ProductId);
        activity?.SetTag("product.name", productUpdateRequest.DisplayName);
        activity?.SetTag("product.unitPrice", productUpdateRequest.UnitPrice);
        activity?.SetTag("product.quantityInStock", productUpdateRequest.QuantityInStock);

        var scopeItems = new Dictionary<string, object>
        {
            ["ProductId"] = productUpdateRequest.ProductId,
            ["DisplayName"] = productUpdateRequest.DisplayName!
        };

        using (_logger.BeginScope(scopeItems))
        {
            try
            {
                _logger.LogInformation("Starting product update process");
                ProductUpdateResult response = await _innerService.UpdateProductAsync(productUpdateRequest, idempotencyKey);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.UpdateProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);
                _logger.LogInformation(
                    "Product operation {Outcome} in {ElapsedMs} ms",
                    response.IsReplay ? "replayed" : "committed", stopwatch.Elapsed.TotalMilliseconds);
                // Trace Instrumentation
                activity?.SetTag("product.updated", !response.IsReplay);
                activity?.SetTag("product.operation.committed", true);
                activity?.SetTag("idempotency.outcome", response.IsReplay ? "replayed" : "updated");
                activity?.SetTag("idempotency.replayed", response.IsReplay);
                activity?.SetTag("idempotency.source", response.Source.ToString().ToLowerInvariant());

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                if (ex is IdempotencyPayloadConflictException)
                {
                    activity?.SetTag("idempotency.outcome", "payload_conflict");
                    activity?.SetTag("idempotency.replayed", false);
                }
                _logger.LogError(ex, "Error occurred during product update flow");
                // Trace Instrumentation
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}
