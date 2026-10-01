using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.ServiceContracts;

public interface IProductsUpdaterService
{
    /// <summary>
    /// Updates the existing product based on the ProductId
    /// </summary>
    /// <param name="productUpdateRequest">Product data to update</param>
    /// <returns>Returns the product after a successful update.</returns>
    Task<ProductUpdateResult> UpdateProductAsync(ProductUpdateRequest productUpdateRequest, Guid idempotencyKey);
}
