using CommonService.Messages;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.Services;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Messaging;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;
using ProductsMicroservice.Infrastructure.Repositories;

namespace ProductsServiceUnitTests;

public sealed class EfProductsPersistenceTests
{
    [Theory]
    [InlineData("Apple iPhone")]
    [InlineData("APPLE IPHONE")]
    [InlineData("APPLEIPHONE")]
    public async Task SaveChangesAsync_ShouldRejectProductsWithTheSameNormalizedName(string conflictingDisplayName)
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        dbContext.Products.Add(CreateProduct("Apple iPhone"));
        await dbContext.SaveChangesAsync();
        dbContext.Products.Add(CreateProduct(conflictingDisplayName));

        Func<Task> action = () => dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldKeepSimplifiedAndTraditionalChineseNamesDistinct()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        dbContext.Products.AddRange(CreateProduct("苹果手机"), CreateProduct("蘋果手機"));

        await dbContext.SaveChangesAsync();

        dbContext.Products.Add(CreateProduct("苹果 手机"));
        Func<Task> action = () => dbContext.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProductNameExistsAsync_ShouldExcludeTheProductBeingUpdated()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        Product first = CreateProduct("Phone");
        Product second = CreateProduct("Tablet");
        dbContext.Products.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        var repository = new ProductsRepository(dbContext);

        (await repository.ProductNameExistsAsync(first.ProductName, first.ProductId))
            .Should().BeFalse();
        (await repository.ProductNameExistsAsync(first.ProductName, second.ProductId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistProductAndOutboxAtomically()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using var dbContext = database.CreateContext();
        var repository = new ProductsRepository(dbContext);
        var outboxWriter = new ProductOperationOutboxWriter(dbContext);
        var product = CreateProduct("Phone");
        ProductOperationResultMessage message = CreateMessage(product.ProductId);

        await repository.AddProductAsync(product);
        await outboxWriter.WriteAsync(message);
        await dbContext.SaveChangesAsync();

        await using ApplicationDbContext verificationContext = database.CreateContext();
        Product persistedProduct = await verificationContext.Products.SingleAsync();
        ProductOperationOutbox persistedOutbox =
            await verificationContext.ProductOperationOutbox.SingleAsync();
        persistedProduct.Version.Should().Be(1);
        persistedOutbox.NotificationId.Should().Be(message.NotificationId);
    }

    [Fact]
    public async Task DisposingWithoutSave_ShouldDiscardTrackedProductAndOutbox()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        var product = CreateProduct("Phone");
        await using (var dbContext = database.CreateContext())
        {
            var repository = new ProductsRepository(dbContext);
            var outboxWriter = new ProductOperationOutboxWriter(dbContext);
            await repository.AddProductAsync(product);
            await outboxWriter.WriteAsync(CreateMessage(product.ProductId));
        }

        await using ApplicationDbContext verificationContext = database.CreateContext();
        (await verificationContext.Products.AnyAsync()).Should().BeFalse();
        (await verificationContext.ProductOperationOutbox.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldIncrementVersionAndCommitWithOutbox()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        await using (ApplicationDbContext seedContext = database.CreateContext())
        {
            seedContext.Products.Add(new Product
            {
                ProductId = productId,
                ProductName = "Original",
                DisplayName = "Original",
                UnitPrice = 1m,
                QuantityInStock = 0,
                Version = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = database.CreateContext();
        var repository = new ProductsRepository(dbContext);
        var outboxWriter = new ProductOperationOutboxWriter(dbContext);
        Product? updated = await repository.UpdateProductAsync(new Product
        {
            ProductId = productId,
            ProductName = "UPDATED",
            DisplayName = "Updated",
            UnitPrice = 1m,
            QuantityInStock = 0,
            Version = 1
        });
        await outboxWriter.WriteAsync(CreateMessage(productId, ProductOperation.Update));
        await dbContext.SaveChangesAsync();

        updated.Should().NotBeNull();
        await using ApplicationDbContext verificationContext = database.CreateContext();
        Product persisted = await verificationContext.Products.SingleAsync();
        persisted.ProductName.Should().Be("UPDATED");
        persisted.DisplayName.Should().Be("Updated");
        persisted.Version.Should().Be(2);
        (await verificationContext.ProductOperationOutbox.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldCommitDeletionWithOutbox()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        await using (ApplicationDbContext seedContext = database.CreateContext())
        {
            seedContext.Products.Add(new Product
            {
                ProductId = productId,
                ProductName = "To delete",
                DisplayName = "To delete",
                UnitPrice = 1m,
                QuantityInStock = 0,
                Version = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = database.CreateContext();
        var repository = new ProductsRepository(dbContext);
        var outboxWriter = new ProductOperationOutboxWriter(dbContext);
        Product? deleted = await repository.DeleteProductAsync(productId, 1);
        await outboxWriter.WriteAsync(CreateMessage(productId, ProductOperation.Delete));
        await dbContext.SaveChangesAsync();

        deleted.Should().NotBeNull();
        await using ApplicationDbContext verificationContext = database.CreateContext();
        (await verificationContext.Products.AnyAsync()).Should().BeFalse();
        (await verificationContext.ProductOperationOutbox.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenProductInsertFails_ShouldNotPersistOutbox()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        await using (ApplicationDbContext seedContext = database.CreateContext())
        {
            seedContext.Products.Add(new Product
            {
                ProductId = productId,
                ProductName = "Existing",
                DisplayName = "Existing",
                UnitPrice = 1m,
                QuantityInStock = 0,
                Version = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = database.CreateContext();
        var repository = new ProductsRepository(dbContext);
        var outboxWriter = new ProductOperationOutboxWriter(dbContext);
        await repository.AddProductAsync(new Product
        {
            ProductId = productId,
            ProductName = "DUPLICATE",
            DisplayName = "Duplicate",
            UnitPrice = 1m,
            QuantityInStock = 0
        });
        await outboxWriter.WriteAsync(CreateMessage(productId));

        Func<Task> action = () => dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
        await using ApplicationDbContext verificationContext = database.CreateContext();
        (await verificationContext.ProductOperationOutbox.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenAnotherCommitChangesVersion_ShouldThrowConcurrencyException()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        await using (ApplicationDbContext seedContext = database.CreateContext())
        {
            seedContext.Products.Add(new Product
            {
                ProductId = productId,
                ProductName = "Original",
                DisplayName = "Original",
                UnitPrice = 1m,
                QuantityInStock = 0,
                Version = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using ApplicationDbContext firstContext = database.CreateContext();
        await using ApplicationDbContext secondContext = database.CreateContext();
        var firstRepository = new ProductsRepository(firstContext);
        var secondRepository = new ProductsRepository(secondContext);
        await firstRepository.UpdateProductAsync(new Product { ProductId = productId, ProductName = "FIRST", DisplayName = "First", UnitPrice = 1m, QuantityInStock = 0, Version = 1 });
        await secondRepository.UpdateProductAsync(new Product { ProductId = productId, ProductName = "SECOND", DisplayName = "Second", UnitPrice = 1m, QuantityInStock = 0, Version = 1 });
        await firstContext.SaveChangesAsync();

        Func<Task> action = () => secondContext.SaveChangesAsync();

        await action.Should().ThrowAsync<ProductConcurrencyException>();
    }

    [Fact]
    public async Task UpdateAndDeleteAsync_WhenProductDoesNotExist_ShouldReturnNull()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using var dbContext = database.CreateContext();
        var repository = new ProductsRepository(dbContext);
        Guid productId = Guid.NewGuid();

        Product? updated = await repository.UpdateProductAsync(new Product { ProductId = productId });
        Product? deleted = await repository.DeleteProductAsync(productId, 1);

        updated.Should().BeNull();
        deleted.Should().BeNull();
    }

    private static ProductOperationResultMessage CreateMessage(
        Guid productId,
        ProductOperation operation = ProductOperation.Add) => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        "correlation-id",
        "user-id",
        "user@example.com",
        "en-US",
        operation,
        ProductOperationStatus.Success,
        productId,
        "Phone",
        1,
        null);

    private static Product CreateProduct(string displayName)
    {
        ProductNames names = ProductNameNormalizer.Normalize(displayName);
        return new Product
        {
            ProductId = Guid.NewGuid(),
            DisplayName = names.DisplayName,
            ProductName = names.ProductName,
            UnitPrice = 1m,
            QuantityInStock = 0
        };
    }
}
