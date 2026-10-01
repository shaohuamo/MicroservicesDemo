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
    private readonly Guid _key = Guid.NewGuid();
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
        Func<Task> act = () => _decorator.DeleteProductAsync(Guid.Empty, 1, _key);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteProductAsync_WhenReplayed_ShouldSkipAllCacheOperations()
    {
        Guid productId = Guid.NewGuid();
        var replay = new ProductDeleteResult(true, true) { Source = IdempotencyResultSource.Database };
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ReturnsAsync(replay);

        (await _decorator.DeleteProductAsync(productId, 1, _key)).Should().BeSameAs(replay);

        _cacheMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldInvalidateDetailAndListCaches_WhenSuccessful()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key))
            .Callback(() => _cacheMock.Verify(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never))
            .ReturnsAsync(new ProductDeleteResult(true, false));

        await _decorator.DeleteProductAsync(productId, 1, _key);

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CachedProduct(productId));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue is null ? null : Encoding.UTF8.GetBytes(cachedValue));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(
                ProductCacheKeys.GetDetailsKey(productId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);
        if (notFound)
        {
            _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CachedProduct(productId));
        }
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

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
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeleteProductAsync_ShouldComplete_WhenCacheInvalidationFails(
        bool detailFails, bool listFails)
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key))
            .ReturnsAsync(new ProductDeleteResult(true, false));
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(productId), It.IsAny<CancellationToken>()))
            .Returns(detailFails
                ? Task.FromException(new InvalidOperationException("detail cache unavailable"))
                : Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listFails
                ? Task.FromException(new InvalidOperationException("list cache unavailable"))
                : Task.CompletedTask);

        await _decorator.DeleteProductAsync(productId, 1, _key);

        _innerMock.Verify(x => x.DeleteProductAsync(productId, 1, _key), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Parallel invalidation

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProductAsync_StartsBothRemovalsBeforeEitherCompletes(bool versionConflict)
    {
        var productId = Guid.NewGuid();
        var detailKey = ProductCacheKeys.GetDetailsKey(productId);
        var detailRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new ProductConcurrencyException(productId);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key))
            .Returns(versionConflict ? Task.FromException<ProductDeleteResult>(failure) : Task.FromResult(new ProductDeleteResult(true, false)));
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Returns(detailRemoval.Task);
        _cacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listRemoval.Task);

        var operation = Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));
        try
        {
            _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
            _cacheMock.Verify(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey,
                It.IsAny<CancellationToken>()), Times.Once);
            operation.IsCompleted.Should().BeFalse();
            detailRemoval.SetResult();
            operation.IsCompleted.Should().BeFalse("the list removal is still pending");
        }
        finally
        {
            detailRemoval.TrySetResult();
            listRemoval.TrySetResult();
            await operation;
        }

        (await operation).Should().BeSameAs(versionConflict ? failure : null);
    }

    [Fact]
    public async Task DeleteProductAsync_NotFound_StartsDetailLookupWhileListRemovalIsPending()
    {
        var productId = Guid.NewGuid();
        var detailKey = ProductCacheKeys.GetDetailsKey(productId);
        var failure = new ProductNotFoundException(productId);
        var listRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookup = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var detailRemovalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listRemoval.Task);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>())).Returns(lookup.Task);
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Callback(() => detailRemovalStarted.TrySetResult()).Returns(Task.CompletedTask);

        var operation = Record.ExceptionAsync(() => _decorator.DeleteProductAsync(productId, 1, _key));
        try
        {
            _cacheMock.Verify(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
            _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Never);
            lookup.SetResult(CachedProduct(productId));
            await detailRemovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            operation.IsCompleted.Should().BeFalse("the list removal is still pending");
        }
        finally
        {
            lookup.TrySetResult(null);
            listRemoval.TrySetResult();
            await operation;
        }

        (await operation).Should().BeSameAs(failure);
    }

    #endregion
}
