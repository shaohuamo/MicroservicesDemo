using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ProductsMicroservice.Core;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public sealed class ProductIdempotencyRegistrationTests
{
    [Fact]
    public async Task RegisteredWritePipeline_ShouldResolveAllServices_AndCommitBeforeInvalidationWithoutRepeatingSideEffects()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddProductsMicroserviceCore(configuration);
        services.ProductsMicroserviceInfrastructure(configuration);
        var repository = new Mock<IProductsRepository>();
        var records = new Mock<IIdempotencyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var outbox = new Mock<IProductOperationOutboxWriter>();
        var cache = new Mock<IDistributedCache>();
        var accessor = new Mock<IProductOperationContextAccessor>();
        accessor.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user", "user@example.com", "en", "test"));
        IdempotencyRecord? stored = null;
        records.Setup(x => x.GetAsync("user", IdempotencyOperation.AddProduct, It.IsAny<Guid>(), default))
            .ReturnsAsync(() => stored);
        records.Setup(x => x.Add(It.IsAny<IdempotencyRecord>())).Callback<IdempotencyRecord>(record => stored = record);
        repository.Setup(x => x.AddProductAsync(It.IsAny<Product>(), default))
            .ReturnsAsync((Product product, CancellationToken _) => { product.Version = 1; return product; });
        unitOfWork.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), default)).Callback(() =>
            unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once)).Returns(Task.CompletedTask);
        services.AddScoped(_ => repository.Object);
        services.AddScoped(_ => records.Object);
        services.AddScoped(_ => unitOfWork.Object);
        services.AddScoped(_ => outbox.Object);
        services.AddScoped(_ => accessor.Object);
        services.AddSingleton(cache.Object);
        await using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IProductsUpdaterService>().Should().BeOfType<ProductsUpdaterTelemetryDecorator>();
        scope.ServiceProvider.GetRequiredService<IProductsDeleterService>().Should().BeOfType<ProductsDeleterTelemetryDecorator>();
        var adder = scope.ServiceProvider.GetRequiredService<IProductsAdderService>();
        adder.Should().BeOfType<ProductsAdderTelemetryDecorator>();
        var request = new ProductAddRequest { DisplayName = "Product", UnitPrice = 10, QuantityInStock = 2 };
        Guid key = Guid.NewGuid();

        ProductAddResult success = await adder.AddProductAsync(request, key);
        ProductAddResult replay = await adder.AddProductAsync(request, key);

        success.IsReplay.Should().BeFalse();
        replay.Source.Should().Be(IdempotencyResultSource.Database);
        replay.Product.Should().BeEquivalentTo(success.Product);
        unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        outbox.Verify(x => x.WriteAsync(It.IsAny<CommonService.Messages.ProductOperationResultMessage>(), default), Times.Once);
        cache.Verify(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, default), Times.Once);
    }
}
