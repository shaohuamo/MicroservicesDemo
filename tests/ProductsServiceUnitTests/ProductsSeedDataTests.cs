using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
}
