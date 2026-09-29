using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Diagnostics;
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

    public async Task<ProductResponse> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

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
                ProductResponse response = await _innerService.UpdateProductAsync(productUpdateRequest);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.UpdateProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);
                _logger.LogInformation(
                    "Product and its outbox notification committed in {ElapsedMs} ms",
                    stopwatch.Elapsed.TotalMilliseconds);
                // Trace Instrumentation
                activity?.SetTag("product.updated", true);
                activity?.SetTag("product.operation.committed", true);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error occurred during product update flow");
                // Trace Instrumentation
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}
