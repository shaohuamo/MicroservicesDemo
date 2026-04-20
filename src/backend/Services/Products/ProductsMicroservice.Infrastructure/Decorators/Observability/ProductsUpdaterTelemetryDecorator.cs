using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.Diagnostics;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability
{
    public class ProductsUpdaterTelemetryDecorator : IProductsUpdaterService
    {
        private readonly IProductsUpdaterService _innerService;
        private readonly ILogger<ProductsUpdaterTelemetryDecorator> _logger;

        public ProductsUpdaterTelemetryDecorator(IProductsUpdaterService innerService, 
            ILogger<ProductsUpdaterTelemetryDecorator> logger)
        {
            _innerService = innerService;
            _logger = logger;
        }

        public async Task<ProductResponse?> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
        {
            ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

            var activity = Activity.Current;
            activity?.AddEvent(new("Update Product"));
            activity?.SetTag("product.id", productUpdateRequest.ProductId);
            activity?.SetTag("product.name", productUpdateRequest.ProductName);
            activity?.SetTag("product.unitPrice", productUpdateRequest.UnitPrice);
            activity?.SetTag("product.quantityInStock", productUpdateRequest.QuantityInStock);

            var scopeItems = new Dictionary<string, object>
            {
                ["ProductId"] = productUpdateRequest.ProductId!,
                ["ProductName"] = productUpdateRequest.ProductName!
            };

            using (_logger.BeginScope(scopeItems))
            {
                try
                {
                    _logger.LogInformation("Starting product update process");

                    var response = await _innerService.UpdateProductAsync(productUpdateRequest);

                    if (response == null)
                    {
                        _logger.LogWarning("Product update failed or product not found");
                        activity?.SetTag("product.updated", false);
                        activity?.SetTag("db.result", "not_found");
                    }
                    else
                    {
                        _logger.LogInformation("Product updated successfully");
                        activity?.SetTag("product.updated", true);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during product update flow");
                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    throw;//exception will be handled by ExceptionHandlingMiddleware
                }
            }
        }
    }
}
