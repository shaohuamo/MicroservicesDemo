using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Options;
using System.Text;
using System.Text.Json;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterCachingDecoratorTests
{
    private readonly Mock<IProductsUpdaterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<IDistributedCache> _delayedCacheMock = new();
    private readonly Mock<ILogger<ProductsUpdaterCachingDecorator>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly TaskCompletionSource _delayedDeleteCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProductsUpdaterCachingDecoratorTests()
    {
        _cacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Callback(() => _delayedDeleteCompleted.TrySetResult())
            .Returns(Task.CompletedTask);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IDistributedCache)))
            .Returns(_delayedCacheMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<ProductsUpdaterCachingDecorator>)))
            .Returns(_loggerMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(x => x.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
    }

    private ProductsUpdaterCachingDecorator CreateDecorator()
    {
        return new ProductsUpdaterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            Options.Create(new RedisOptions { DelayedDeleteMs = 0 }),
            Options.Create(new CacheOptions { NullValuePlaceholder = "missing" }),
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    private static byte[] CachedProduct(Guid productId) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new ProductResponse(productId, "Cached", 1, 1, 1)));

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => CreateDecorator().UpdateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldNotRemoveCachesBeforeCallingInnerService()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _innerMock.Setup(x => x.UpdateProductAsync(request))
            .Callback(() => _cacheMock.Verify(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never))
            .ReturnsAsync(response);

        var result = await CreateDecorator().UpdateProductAsync(request);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldInvalidateBothCaches_WhenVersionConflicts()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductConcurrencyException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _innerMock.Verify(x => x.UpdateProductAsync(request), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.GetAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldInvalidateBothCaches_WhenNotFoundAndDetailCached()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CachedProduct(request.ProductId));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public async Task UpdateProductAsync_ShouldInvalidateListOnly_WhenNotFoundWithoutPositiveDetail(string? cachedValue)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue is null ? null : Encoding.UTF8.GetBytes(cachedValue));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            detailKey, It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldKeepNotFoundAndInvalidateList_WhenCacheLookupFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateProductAsync_ShouldKeepOriginalFailureAndTryBothKeys_WhenRemovalFails(bool notFound)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        Exception failure = notFound
            ? new ProductNotFoundException(request.ProductId)
            : new ProductConcurrencyException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);
        if (notFound)
        {
            _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CachedProduct(request.ProductId));
        }
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldNotInvalidateCaches_WhenNameConflictOccurs()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductAlreadyExistsException("duplicate");
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRemoveCachesImmediatelyAndAfterDelay_WhenSuccessful()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 20, 3);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);

        var result = await CreateDecorator().UpdateProductAsync(request);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnResponseAndScheduleDelay_WhenImmediateInvalidationFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);

        var result = await CreateDecorator().UpdateProductAsync(request);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldTryBothDelayedKeys_WhenFirstDelayedRemovalFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var result = await CreateDecorator().UpdateProductAsync(request);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(response);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}
