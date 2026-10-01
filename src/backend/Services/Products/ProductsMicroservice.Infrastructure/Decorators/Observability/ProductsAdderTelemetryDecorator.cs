using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability;

public class ProductsAdderTelemetryDecorator : IProductsAdderService
{
    private readonly IProductsAdderService _inner;
    private readonly ILogger<ProductsAdderTelemetryDecorator> _logger;

    public ProductsAdderTelemetryDecorator(
        IProductsAdderService inner,
        ILogger<ProductsAdderTelemetryDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<ProductAddResult> AddProductAsync(
        ProductAddRequest productAddRequest,
        Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);

        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

        // Trace Instrumentation
        activity?.SetTag("idempotency.key", idempotencyKey.ToString("D"));
        activity?.SetTag("idempotency.operation", "AddProduct");
        activity?.SetTag("product.name", productAddRequest.DisplayName);
        activity?.SetTag("product.unitPrice", productAddRequest.UnitPrice);
        activity?.SetTag("product.quantityInStock", productAddRequest.QuantityInStock);

        var scopeItems = new Dictionary<string, object>
        {
            ["DisplayName"] = productAddRequest.DisplayName!,
            ["UnitPrice"] = productAddRequest.UnitPrice!,
            ["QuantityInStock"] = productAddRequest.QuantityInStock!
        };

        using (_logger.BeginScope(scopeItems))
        {
            try
            {
                _logger.LogInformation("Entering AddProduct pipeline");
                ProductAddResult result = await _inner.AddProductAsync(
                    productAddRequest, idempotencyKey);
                stopwatch.Stop();

                // Metric Instrumentation
                DiagnosticsConfig.AddProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);
                if (!result.IsReplay)
                {
                    DiagnosticsConfig.ProductsCounter.Add(1,
                        new KeyValuePair<string, object?>("product.id", result.Product.ProductId),
                        new("status", "success"));
                }
                // Trace Instrumentation
                activity?.SetTag("product.id", result.Product.ProductId);
                activity?.SetTag("product.operation.committed", true);
                activity?.SetTag("idempotency.outcome",
                    result.IsReplay ? "replayed" : "created");
                activity?.SetTag("idempotency.replayed", result.IsReplay);
                activity?.SetTag("idempotency.source", result.Source.ToString().ToLowerInvariant());

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
                _logger.LogError(ex, "Error in AddProduct pipeline for {DisplayName}",
                    productAddRequest.DisplayName);
                // Trace Instrumentation
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}
