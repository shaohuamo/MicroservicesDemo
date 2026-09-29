using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.Repositories;

/// <summary>
/// EF Core repository. Commands and the transactional outbox writer share the same scoped
/// DbContext, so all tracked changes commit in one SaveChanges call.
/// </summary>
internal sealed class ProductsRepository : IProductsRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ProductsRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ProductNameExistsAsync(
        string productName,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        return _dbContext.Products.AsNoTracking().AnyAsync(
            product => product.ProductName == productName &&
                (!excludingProductId.HasValue || product.ProductId != excludingProductId.Value),
            cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        return await _dbContext.Products.ToListAsync();
    }

    public async Task<Product?> GetProductByProductIdAsync(Guid productId)
    {
        return await _dbContext.Products.FirstOrDefaultAsync(
            product => product.ProductId == productId);
    }

    public Task<Product> AddProductAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        product.Version = 1;
        _dbContext.Products.Add(product);
        return Task.FromResult(product);
    }

    public async Task<Product?> UpdateProductAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        Product? existingProduct = await _dbContext.Products.FindAsync(
            [product.ProductId], cancellationToken);
        if (existingProduct is null)
        {
            return null;
        }

        if (existingProduct.Version != product.Version)
        {
            throw new ProductConcurrencyException(product.ProductId);
        }

        _dbContext.Entry(existingProduct).Property(current => current.Version).OriginalValue =
            product.Version;
        existingProduct.ProductName = product.ProductName;
        existingProduct.DisplayName = product.DisplayName;
        existingProduct.UnitPrice = product.UnitPrice;
        existingProduct.QuantityInStock = product.QuantityInStock;
        existingProduct.Version = checked(product.Version + 1);
        return existingProduct;
    }

    public async Task<Product?> DeleteProductAsync(
        Guid productId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var existingProduct = await _dbContext.Products.FindAsync([productId], cancellationToken);
        if (existingProduct is null)
        {
            return null;
        }

        if (existingProduct.Version != expectedVersion)
        {
            throw new ProductConcurrencyException(productId);
        }

        _dbContext.Entry(existingProduct).Property(product => product.Version).OriginalValue =
            expectedVersion;
        _dbContext.Products.Remove(existingProduct);
        return existingProduct;
    }
}
