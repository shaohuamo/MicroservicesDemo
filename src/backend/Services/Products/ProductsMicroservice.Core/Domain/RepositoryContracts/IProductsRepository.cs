using ProductsMicroservice.Core.Domain.Entities;

namespace ProductsMicroservice.Core.Domain.RepositoryContracts;

/// <summary>
/// EF Core product repository. Mutations are tracked until
/// <see cref="IUnitOfWork.SaveChangesAsync"/> commits product and outbox changes atomically.
/// </summary>
public interface IProductsRepository
{
    /// <summary>
    /// Retrieves all products available in the catalog.
    /// </summary>
    /// <returns>An enumerable collection of products; empty when no products exist.</returns>
    Task<IEnumerable<Product>> GetProductsAsync();

    /// <summary>
    /// Retrieves a product by its identifier.
    /// </summary>
    /// <param name="productId">The identifier of the product to retrieve.</param>
    /// <returns>The matching product, or <see langword="null"/> when it does not exist.</returns>
    Task<Product?> GetProductByProductIdAsync(Guid productId);

    /// <summary>
    /// Determines whether a normalized product name is already in use.
    /// </summary>
    /// <param name="productName">The normalized product name.</param>
    /// <param name="excludingProductId">A product to exclude when checking an update.</param>
    Task<bool> ProductNameExistsAsync(
        string productName,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a product within the active unit of work.
    /// </summary>
    Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing product within the active unit of work.
    /// </summary>
    /// <returns>The updated product, or <see langword="null"/> when it does not exist.</returns>
    Task<Product?> UpdateProductAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a product within the active unit of work.
    /// </summary>
    /// <returns>The deleted product, or <see langword="null"/> when it does not exist.</returns>
    Task<Product?> DeleteProductAsync(
        Guid productId,
        int expectedVersion,
        CancellationToken cancellationToken = default);
}
