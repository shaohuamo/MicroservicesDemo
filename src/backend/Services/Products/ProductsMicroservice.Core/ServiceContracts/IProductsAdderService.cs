using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.ServiceContracts
{
    public interface IProductsAdderService
    {
        /// <summary>
        /// Adds (inserts) product into the table using products repository
        /// </summary>
        /// <param name="productAddRequest">Product to insert</param>
        /// <returns>Product after inserting or null if unsuccessful</returns>
        Task<ProductResponse?> AddProductAsync(ProductAddRequest productAddRequest);
    }
}
