using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.ServiceContracts;
using System.Diagnostics;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability
{
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

        public async Task<bool> DeleteProductAsync(Guid productId)
        {
            // Defensive programming: validate input parameters
            if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));

            var activity = Activity.Current;
            activity?.AddEvent(new("Remove Product By ProductId"));
            activity?.SetTag("product.id", productId);

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
            {
                try
                {
                    _logger.LogInformation("Product Deletion Started");
                    bool result = await _inner.DeleteProductAsync(productId);

                    activity?.SetTag("db.result", result ? "success" : "not_found");
                    activity?.AddEvent(new("Product Deletion Finished"));

                    return result;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Uncaught error during product deletion for {ProductId}", productId);
                    activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                    activity?.AddException(e);
                    throw;//exception will be handled by ExceptionHandlingMiddleware
                }
            }
        }
    }
}
