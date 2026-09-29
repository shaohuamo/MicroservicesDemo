using System.Text.Json;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Services;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.SeedData;

public static class ProductsSeedData
{
    private const string ResourceName =
        "ProductsMicroservice.Infrastructure.SeedData.products.json";

    public static async Task SeedAsync(
        ApplicationDbContext dbContext,
        IDistributedLockProvider? lockProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (lockProvider is null)
        {
            await SeedCoreAsync(dbContext, cancellationToken);
            return;
        }

        IDistributedLock seedLock = lockProvider.CreateLock("lock:products-seed-data");
        await using (await seedLock.AcquireAsync(TimeSpan.FromMinutes(1)))
        {
            // Recheck the database after acquiring the lock so waiting pods skip the insert.
            await SeedCoreAsync(dbContext, cancellationToken);
        }
    }

    private static async Task SeedCoreAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Product> products = await LoadAsync(cancellationToken);
        Guid[] productIds = products.Select(product => product.ProductId).ToArray();
        HashSet<Guid> existingProductIds = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.ProductId))
            .Select(product => product.ProductId)
            .ToHashSetAsync(cancellationToken);

        Product[] missingProducts = products
            .Where(product => !existingProductIds.Contains(product.ProductId))
            .ToArray();
        if (missingProducts.Length == 0)
        {
            return;
        }

        await dbContext.Products.AddRangeAsync(missingProducts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<Product>> LoadAsync(CancellationToken cancellationToken)
    {
        await using Stream stream = typeof(ProductsSeedData).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource '{ResourceName}' was not found.");

        SeedProduct[] seedProducts = await JsonSerializer.DeserializeAsync<SeedProduct[]>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken)
            ?? throw new InvalidOperationException("Products seed data is empty or invalid.");

        if (seedProducts.Any(product => product.ProductId == Guid.Empty ||
                                        string.IsNullOrWhiteSpace(product.DisplayName)))
        {
            throw new InvalidOperationException("Products seed data contains an invalid product.");
        }

        return seedProducts.Select(seedProduct =>
        {
            ProductNames names = ProductNameNormalizer.Normalize(seedProduct.DisplayName);
            return new Product
            {
                ProductId = seedProduct.ProductId,
                DisplayName = names.DisplayName,
                ProductName = names.ProductName,
                UnitPrice = seedProduct.UnitPrice,
                QuantityInStock = seedProduct.QuantityInStock,
                Version = seedProduct.Version
            };
        }).ToArray();
    }

    private sealed class SeedProduct
    {
        public Guid ProductId { get; init; }
        public string? DisplayName { get; init; }
        public decimal UnitPrice { get; init; }
        public int QuantityInStock { get; init; }
        public int Version { get; init; }
    }
}
