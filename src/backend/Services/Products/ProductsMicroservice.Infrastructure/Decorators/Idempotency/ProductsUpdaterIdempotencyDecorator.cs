using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal sealed class ProductsUpdaterIdempotencyDecorator(
    IProductsUpdaterService inner, ProductIdempotencyExecutor executor) : IProductsUpdaterService
{
    public async Task<ProductUpdateResult> UpdateProductAsync(ProductUpdateRequest productUpdateRequest, Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);
        var result = await executor.ExecuteAsync(IdempotencyOperation.UpdateProduct, idempotencyKey,
            ProductRequestFingerprint.ForUpdate(productUpdateRequest), 200,
            async () => (await inner.UpdateProductAsync(productUpdateRequest, idempotencyKey)).Product);
        return new ProductUpdateResult(result.Response, result.IsReplay) { Source = result.Source };
    }
}
