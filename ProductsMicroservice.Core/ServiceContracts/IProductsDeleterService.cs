namespace ProductsMicroservice.Core.ServiceContracts;

public interface IProductsDeleterService
{
    /// <summary>
    /// Deletes an existing product based on given product id
    /// </summary>
    /// <param name="productId">ProductId to search and delete</param>
    /// <returns>Returns true if the deletion is successful; otherwise false</returns>
    Task<bool> DeleteProductAsync(Guid productId);
}