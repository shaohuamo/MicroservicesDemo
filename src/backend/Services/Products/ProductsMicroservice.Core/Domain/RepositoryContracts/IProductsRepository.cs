using ProductsMicroservice.Core.Domain.Entities;
using System.Linq.Expressions;

namespace ProductsMicroservice.Core.Domain.RepositoryContracts;

/// <summary>
/// Represents a repository for managing 'products' table
/// </summary>
public interface IProductsRepository
{
    /// <summary>
    /// Retrieves all products asynchronously
    /// </summary>
    /// <returns>Returns all products from the table</returns>
    Task<IEnumerable<Product>> GetProductsAsync();

    /// <summary>
    /// Retrieves a single product based on the specified condition asynchronously
    /// </summary>
    /// <param name="productId"></param>
    /// <returns>Returns a single product or null if not found</returns>
    Task<Product?> GetProductByProductIdAsync(Guid productId);

    /// <summary>
    /// Adds a new product into the products table asynchronously
    /// </summary>
    /// <param name="product">The product to be added</param>
    /// <returns>Returns the added product object or null if unsuccessful</returns>
    Task<Product?> AddProductAsync(Product product);

    /// <summary>
    /// Updates an existing product asynchronously.
    /// </summary>
    /// <param name="product">The product to be updated</param>
    /// <returns>Returns the updated product; or null if not found</returns>
    Task<Product?> UpdateProductAsync(Product product);

    /// <summary>
    /// Deletes the product asynchronously
    /// </summary>
    /// <param name="productId">The product ID to be deleted</param>
    /// <returns>Returns true if the deletion is successful, false otherwise.</returns>
    Task<bool> DeleteProductAsync(Guid productId);
}