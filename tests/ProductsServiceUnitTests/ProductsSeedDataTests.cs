using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.SeedData;

namespace ProductsServiceUnitTests;

public sealed class ProductsSeedDataTests
{
    [Fact]
    public async Task SeedAsync_ShouldInsertSampleProductsOnlyOnce()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();

        await ProductsSeedData.SeedAsync(dbContext);
        await ProductsSeedData.SeedAsync(dbContext);

        (await dbContext.Products.CountAsync()).Should().Be(12);
        (await dbContext.Products.SingleAsync(product => product.DisplayName == "Apple iPhone 15 Pro Max"))
            .ProductName.Should().Be("APPLEIPHONE15PROMAX");
    }

    [Fact]
    public async Task SeedAsync_WhenProductAlreadyExists_DoesNotOverwriteIt()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        await ProductsSeedData.SeedAsync(dbContext);
        Product existing = await dbContext.Products.FirstAsync();
        existing.QuantityInStock = 12345;
        await dbContext.SaveChangesAsync();

        await ProductsSeedData.SeedAsync(dbContext);

        (await dbContext.Products.AsNoTracking()
            .SingleAsync(product => product.ProductId == existing.ProductId))
            .QuantityInStock.Should().Be(12345);
    }

    [Fact]
    public async Task SeedAsync_WhenOtherUniqueKeyConflicts_RollsBackAllSeedInserts()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        dbContext.Products.Add(new Product
        {
            ProductId = Guid.NewGuid(),
            ProductName = "APPLEIPHONE15PROMAX",
            DisplayName = "Existing Product",
            UnitPrice = 1,
            QuantityInStock = 1,
            Version = 1
        });
        await dbContext.SaveChangesAsync();

        Func<Task> act = () => ProductsSeedData.SeedAsync(dbContext);

        await act.Should().ThrowAsync<SqliteException>();
        (await dbContext.Products.CountAsync()).Should().Be(1);
    }
}
