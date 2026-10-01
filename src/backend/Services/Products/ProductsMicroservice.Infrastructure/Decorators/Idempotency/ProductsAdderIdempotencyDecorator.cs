using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal sealed class ProductsAdderIdempotencyDecorator(
    IProductsAdderService inner, ProductIdempotencyExecutor executor) : IProductsAdderService
{
    public async Task<ProductAddResult> AddProductAsync(ProductAddRequest productAddRequest, Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);
        var result = await executor.ExecuteAsync(IdempotencyOperation.AddProduct, idempotencyKey,
            ProductRequestFingerprint.ForAdd(productAddRequest), 201,
            async () => (await inner.AddProductAsync(productAddRequest, idempotencyKey)).Product);
        return new ProductAddResult(result.Response, result.IsReplay) { Source = result.Source };
    }
}
