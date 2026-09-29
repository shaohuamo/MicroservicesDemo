namespace ProductsMicroservice.Core.ServiceContracts;

public interface IProductsDeleterService
{
    /// <summary>
    /// Deletes an existing product based on given product id
    /// </summary>
    /// <param name="productId">ProductId to search and delete</param>
    /// <param name="expectedVersion">Version originally read by the client.</param>
    /// <returns>A task that completes after a successful deletion.</returns>
    Task DeleteProductAsync(Guid productId, int expectedVersion);
}
