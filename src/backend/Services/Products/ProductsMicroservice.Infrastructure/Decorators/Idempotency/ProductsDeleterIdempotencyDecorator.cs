using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal sealed class ProductsDeleterIdempotencyDecorator(
    IProductsDeleterService inner, ProductIdempotencyExecutor executor) : IProductsDeleterService
{
    public async Task<ProductDeleteResult> DeleteProductAsync(Guid productId, int expectedVersion, Guid idempotencyKey)
    {
        if (productId == Guid.Empty) throw new ArgumentException("ProductId cannot be empty", nameof(productId));
        var result = await executor.ExecuteAsync(IdempotencyOperation.DeleteProduct, idempotencyKey,
            ProductRequestFingerprint.ForDelete(productId, expectedVersion), 200,
            async () => (await inner.DeleteProductAsync(productId, expectedVersion, idempotencyKey)).Deleted);
        return new ProductDeleteResult(result.Response, result.IsReplay) { Source = result.Source };
    }
}
