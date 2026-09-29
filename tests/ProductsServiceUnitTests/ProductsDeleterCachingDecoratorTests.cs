using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
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

public class ProductsDeleterCachingDecoratorTests
{
    private readonly Mock<IProductsDeleterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<ProductsDeleterCachingDecorator>> _loggerMock = new();
    private readonly ProductsDeleterCachingDecorator _decorator;

    public ProductsDeleterCachingDecoratorTests()
    {
        _decorator = new ProductsDeleterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            Options.Create(new CacheOptions { NullValuePlaceholder = "missing" }),
            _loggerMock.Object);
    }

    private static byte[] CachedProduct(Guid productId) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new ProductResponse(productId, "Cached", 1, 1, 1)));

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenProductIdIsEmpty()
    {
        Func<Task> act = () => _decorator.DeleteProductAsync(Guid.Empty, 1);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldInvalidateDetailAndListCaches_WhenSuccessful()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1))
            .Callback(() => _cacheMock.Verify(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never))
            .Returns(Task.CompletedTask);

        await _decorator.DeleteProductAsync(productId, 1);

        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldInvalidateBothCaches_WhenVersionConflicts()
    {
        var productId = Guid.NewGuid();
        var failure = new ProductConcurrencyException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(productId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.GetAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldInvalidateBothCaches_WhenNotFoundAndDetailCached()
    {
        var productId = Guid.NewGuid();
        var detailKey = ProductCacheKeys.GetDetailsKey(productId);
        var failure = new ProductNotFoundException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CachedProduct(productId));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public async Task DeleteProductAsync_ShouldInvalidateListOnly_WhenNotFoundWithoutPositiveDetail(string? cachedValue)
    {
        var productId = Guid.NewGuid();
        var detailKey = ProductCacheKeys.GetDetailsKey(productId);
        var failure = new ProductNotFoundException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue is null ? null : Encoding.UTF8.GetBytes(cachedValue));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            detailKey, It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldKeepNotFoundAndInvalidateList_WhenCacheLookupFails()
    {
        var productId = Guid.NewGuid();
        var failure = new ProductNotFoundException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(
                ProductCacheKeys.GetDetailsKey(productId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(productId), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProductAsync_ShouldKeepOriginalFailureAndTryBothKeys_WhenRemovalFails(bool notFound)
    {
        var productId = Guid.NewGuid();
        var detailKey = ProductCacheKeys.GetDetailsKey(productId);
        Exception failure = notFound
            ? new ProductNotFoundException(productId)
            : new ProductConcurrencyException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);
        if (notFound)
        {
            _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CachedProduct(productId));
        }
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldNotInvalidateCaches_WhenUnrelatedFailureOccurs()
    {
        var productId = Guid.NewGuid();
        var failure = new InvalidOperationException("delete failed");
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldComplete_WhenCacheInvalidationFails()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(productId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        await _decorator.DeleteProductAsync(productId, 1);

        _innerMock.Verify(x => x.DeleteProductAsync(productId, 1), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}
