using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.ServiceContracts;

public interface IProductsUpdaterService
{
    /// <summary>
    /// Updates the existing product based on the ProductId
    /// </summary>
    /// <param name="productUpdateRequest">Product data to update</param>
    /// <returns>Returns product object after successful update; otherwise null</returns>
    Task<ProductResponse?> UpdateProductAsync(ProductUpdateRequest productUpdateRequest);
}