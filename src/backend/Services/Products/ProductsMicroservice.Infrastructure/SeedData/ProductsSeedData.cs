using System.Text.Json;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        await SeedCoreAsync(dbContext, cancellationToken);
    }

    private static async Task SeedCoreAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Product> products = await LoadAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (Product product in products)
        {
            // The database resolves a concurrent Job retry atomically. Conflicts on other
            // unique keys still fail, rather than silently hiding inconsistent seed data.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Products"
                    ("ProductId", "ProductName", "DisplayName", "UnitPrice", "QuantityInStock", "Version")
                VALUES
                    ({product.ProductId}, {product.ProductName}, {product.DisplayName},
                     {product.UnitPrice}, {product.QuantityInStock}, {product.Version})
                ON CONFLICT ("ProductId") DO NOTHING
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
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
