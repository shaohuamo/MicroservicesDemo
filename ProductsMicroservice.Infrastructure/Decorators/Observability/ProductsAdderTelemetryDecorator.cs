using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProductsMicroservice.Infrastructure.Decorators.Observability;

public class ProductsAdderTelemetryDecorator : IProductsAdderService
{
    private readonly IProductsAdderService _inner;
    private readonly ILogger<ProductsAdderTelemetryDecorator> _logger;

    public ProductsAdderTelemetryDecorator(IProductsAdderService inner,
        ILogger<ProductsAdderTelemetryDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<ProductResponse?> AddProductAsync(ProductAddRequest productAddRequest)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);//defend against null input

        //010-000:trace instrumentation
        var activity = Activity.Current;
        activity?.SetTag("product.name", productAddRequest.ProductName);
        activity?.SetTag("product.unitPrice", productAddRequest.UnitPrice);
        activity?.SetTag("product.quantityInStock", productAddRequest.QuantityInStock);

        //020-000:log context enrichment
        var scopeItems = new Dictionary<string, object>
        {
            ["ProductName"] = productAddRequest.ProductName!,
            ["UnitPrice"] = productAddRequest.UnitPrice!,
            ["QuantityInStock"] = productAddRequest.QuantityInStock!
        };

        using (_logger.BeginScope(scopeItems))
        {
            try
            {
                _logger.LogInformation("Entering AddProduct pipeline");

                //030-000: Call the inner service to add the product
                var addedProduct = await _inner.AddProductAsync(productAddRequest!);

                if (addedProduct != null)
                {
                    activity?.SetTag("product.id", addedProduct.ProductId);
                    using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = addedProduct.ProductId }))
                    {
                        _logger.LogInformation("Product added successfully in pipeline.");
                    }
                }

                return addedProduct;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddProduct pipeline for {ProductName}", productAddRequest!.ProductName);
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                throw;//exception will be handled by ExceptionHandlingMiddleware
            }
        }
    }
}